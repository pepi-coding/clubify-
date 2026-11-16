import { useEffect, useId, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { IScannerControls } from "@zxing/browser";
import { apiClient, ApiError } from "./api/client";
import type { components } from "./api/schema";
type S = components["schemas"];
type Mode = "admission" | "redemption";
type Path = { tenantId: string; venueId: string; eventId: string };
type Body = S["AdmissionRequest"] | S["RedemptionRequest"];
type Journal = { key: string; fingerprint: string; station?: string };
const date = (s: string) =>
  new Intl.DateTimeFormat("es-PE", {
    dateStyle: "short",
    timeStyle: "medium",
    timeZone: "America/Lima",
  }).format(new Date(s));
const digest = async (value: string) =>
  Array.from(
    new Uint8Array(
      await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value)),
    ),
    (v) => v.toString(16).padStart(2, "0"),
  ).join("");
function message(e: unknown) {
  return e instanceof ApiError
    ? e.message +
        (e.recordedAt ? ` Registrado: ${date(e.recordedAt)} (Lima).` : "")
    : "No se pudo confirmar con el servidor. No autorices ingreso ni entrega. Reintenta o consulta el resultado.";
}
export function OperationsWorkspace({
  tenantId,
  venueId,
  mode,
  actor,
}: {
  tenantId: string;
  venueId: string;
  mode: Mode;
  actor: string;
}) {
  const [eventId, setEventId] = useState("");
  const q = useQuery({
    queryKey: ["operation-events", tenantId, venueId],
    queryFn: async () =>
      (
        await apiClient.GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/events",
          {
            params: { path: { tenantId, venueId } },
            signal: AbortSignal.timeout(10000),
          },
        )
      ).data!,
  });
  return (
    <section className="card operations">
      <p className="eyebrow">
        {mode === "admission"
          ? "SEGURIDAD · INGRESO ÚNICO"
          : "BARRA · ENTREGA PARCIAL"}
      </p>
      <h2>
        {mode === "admission" ? "Control de ingresos" : "Canje de consumos"}
      </h2>
      <p>
        {mode === "admission"
          ? "Consultar un pase no registra ingreso. Confirma solo cuando la persona vaya a entrar."
          : "Leer un vale no entrega consumos. Selecciona las cantidades que entregarás y confirma."}
      </p>
      <label>
        Evento de operación
        <select value={eventId} onChange={(e) => setEventId(e.target.value)}>
          <option value="">Selecciona el evento</option>
          {q.data
            ?.filter((e) => e.status === "Published")
            .map((e) => (
              <option key={e.id} value={e.id}>
                {e.name}
              </option>
            ))}
        </select>
      </label>
      {q.isPending && <p>Cargando eventos…</p>}
      {q.isError && <p role="alert">{q.error.message}</p>}
      {eventId && (
        <Scanner
          key={eventId}
          path={{ tenantId, venueId, eventId }}
          mode={mode}
          actor={actor}
        />
      )}
    </section>
  );
}
function Scanner({
  path,
  mode,
  actor,
}: {
  path: Path;
  mode: Mode;
  actor: string;
}) {
  const inputId = useId();
  const [token, setToken] = useState(""),
    [station, setStation] = useState(
      mode === "admission" ? "Puerta principal" : "Barra principal",
    );
  const [view, setView] = useState<S["ScanOutcome"] | null>(null),
    [error, setError] = useState(""),
    [note, setNote] = useState(""),
    [busy, setBusy] = useState(false),
    [unknown, setUnknown] = useState(false),
    [quantities, setQuantities] = useState<Record<string, number>>({}),
    [sound, setSound] = useState(true),
    [online, setOnline] = useState(navigator.onLine);
  const [camera, setCamera] = useState(false),
    [cameraError, setCameraError] = useState("");
  const video = useRef<HTMLVideoElement>(null),
    controls = useRef<IScannerControls | null>(null),
    generation = useRef(0),
    mounted = useRef(true),
    audio = useRef<AudioContext | null>(null);
  const pending = useRef<Journal | null>(null),
    pendingBody = useRef<Body | null>(null),
    storageKey = useRef<string | null>(null),
    inFlight = useRef(false);
  function armSound() {
    if (sound && typeof AudioContext !== "undefined") {
      audio.current ??= new AudioContext();
      void audio.current.resume().catch(() => {});
    }
  }
  function tone(ok: boolean, inspection = false) {
    if (!sound || !audio.current) return;
    const ctx = audio.current;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.frequency.value = inspection ? 500 : ok ? 880 : 180;
    gain.gain.setValueAtTime(0.08, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.18);
    osc.start();
    osc.stop(ctx.currentTime + 0.18);
  }
  function stopCamera() {
    generation.current++;
    controls.current?.stop();
    controls.current = null;
    const stream = video.current?.srcObject;
    if (stream instanceof MediaStream)
      stream.getTracks().forEach((t) => t.stop());
    if (video.current) video.current.srcObject = null;
    setCamera(false);
  }
  function clearJournal() {
    pending.current = null;
    pendingBody.current = null;
    setUnknown(false);
    try {
      if (storageKey.current) sessionStorage.removeItem(storageKey.current);
    } catch {}
  }
  async function resultFor(key: string) {
    return mode === "admission"
      ? (
          await apiClient.GET(
            "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/admissions/operations/{key}",
            {
              params: { path: { ...path, key } },
              signal: AbortSignal.timeout(10000),
            },
          )
        ).data!
      : (
          await apiClient.GET(
            "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/redemptions/operations/{key}",
            {
              params: { path: { ...path, key } },
              signal: AbortSignal.timeout(10000),
            },
          )
        ).data!;
  }
  async function recover() {
    if (!pending.current || inFlight.current) return;
    inFlight.current = true;
    setBusy(true);
    setView(null);
    setError("");
    armSound();
    try {
      const result = await resultFor(pending.current.key);
      if (!mounted.current) return;
      setView(result);
      setQuantities({});
      setNote(
        "Resultado confirmado por el servidor y recuperado. El saldo corresponde a esta operación; consulta el vale para ver el saldo actual.",
      );
      clearJournal();
      tone(true);
    } catch (e) {
      if (!mounted.current) return;
      if (
        e instanceof ApiError &&
        e.status !== 404 &&
        e.status < 500 &&
        ![401, 403, 429].includes(e.status) &&
        e.code !== "IDEMPOTENCY_CONFLICT"
      ) {
        clearJournal();
        setError(message(e));
        tone(false);
      } else {
        setUnknown(true);
        setError(
          "Confirmación aún sin recuperar. No repitas la entrega ni autorices ingreso sin confirmación. Consulta otra vez o reintenta con el mismo código, estación y cantidades.",
        );
      }
    } finally {
      inFlight.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  useEffect(() => {
    mounted.current = true;
    let active = true;
    void (async () => {
      const key =
        "club.operations." +
        (await digest(
          [actor, path.tenantId, path.venueId, path.eventId, mode].join("|"),
        ));
      if (!active) return;
      storageKey.current = key;
      try {
        const raw = sessionStorage.getItem(key);
        if (raw) {
          const saved = JSON.parse(raw) as Journal;
          if (
            typeof saved.key === "string" &&
            /^[0-9a-f]{64}$/.test(saved.fingerprint)
          ) {
            pending.current = saved;
            if (typeof saved.station === 'string') setStation(saved.station);
            setUnknown(true);
            void recover();
          }
        }
      } catch {}
    })();
    return () => {
      active = false;
      mounted.current = false;
      generation.current++;
      controls.current?.stop();
      const stream = video.current?.srcObject;
      if (stream instanceof MediaStream)
        stream.getTracks().forEach((t) => t.stop());
      void audio.current?.close().catch(() => {});
    };
  }, []);
  useEffect(() => {
    const update = () => {
      setOnline(navigator.onLine);
      if (!navigator.onLine) {
        setView(null);
        setError("Sin conexión. No autorices ingresos ni entregas nuevas.");
      }
    };
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return () => {
      window.removeEventListener("online", update);
      window.removeEventListener("offline", update);
    };
  }, []);
  async function inspect(value = token) {
    if (inFlight.current) return;
    armSound();
    stopCamera();
    inFlight.current = true;
    setBusy(true);
    setView(null);
    setError("");
    setNote("");
    setQuantities({});
    const cleaned = value.trim();
    setToken(cleaned);
    try {
      const result =
        mode === "admission"
          ? (
              await apiClient.POST(
                "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/admissions/inspect",
                {
                  params: { path },
                  body: { token: cleaned },
                  signal: AbortSignal.timeout(10000),
                },
              )
            ).data!
          : (
              await apiClient.POST(
                "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/redemptions/inspect",
                {
                  params: { path },
                  body: { token: cleaned },
                  signal: AbortSignal.timeout(10000),
                },
              )
            ).data!;
      if (!mounted.current) return;
      setView(result);
      tone(false, result.code === "VALID");
    } catch (e) {
      if (mounted.current) {
        setError(message(e));
        tone(false);
      }
    } finally {
      inFlight.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  async function startCamera() {
    armSound();
    setView(null);
    setCameraError("");
    setError("");
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
      setCameraError(
        "La cámara necesita HTTPS o localhost. Puedes pegar o escribir el código.",
      );
      return;
    }
    setCamera(true);
    const id = ++generation.current;
    try {
      const { BrowserQRCodeReader } = await import("@zxing/browser");
      if (!mounted.current || generation.current !== id) return;
      const reader = new BrowserQRCodeReader(undefined, {
        delayBetweenScanAttempts: 180,
      });
      const acquired = await reader.decodeFromConstraints(
        { video: { facingMode: { ideal: "environment" } }, audio: false },
        video.current!,
        (result, _error, scanner) => {
          if (result && mounted.current && generation.current === id) {
            scanner.stop();
            void inspect(result.getText());
          }
        },
      );
      if (!mounted.current || generation.current !== id) acquired.stop();
      else controls.current = acquired;
    } catch (e) {
      if (!mounted.current || generation.current !== id) return;
      setCamera(false);
      setCameraError(
        e instanceof DOMException && e.name === "NotAllowedError"
          ? "Permiso de cámara denegado. Habilítalo en el navegador o pega/escribe el código."
          : "No se pudo abrir la cámara. Puedes pegar o escribir el código.",
      );
    }
  }
  async function confirm(body?: Body) {
    if (inFlight.current) return;
    armSound();
    setError("");
    setNote("");
    const payload =
      body ??
      (mode === "admission"
        ? { token: token.trim(), station }
        : {
            token: token.trim(),
            station,
            lines: Object.entries(quantities)
              .filter(([, q]) => q > 0)
              .sort(([a], [b]) => a.localeCompare(b))
              .map(([entitlementId, quantity]) => ({
                entitlementId,
                quantity,
              })),
          });
    inFlight.current = true;
    setBusy(true);
    setView(null);
    try {
      const fingerprint = await digest(JSON.stringify(payload));
      if (pending.current && pending.current.fingerprint !== fingerprint) {
        setUnknown(true);
        setError(
          "Existe una confirmación pendiente con otros datos. Consulta su resultado o reingresa los mismos datos antes de continuar.",
        );
        return;
      }
      const journal = pending.current ?? {
        key: crypto.randomUUID(),
        fingerprint,
        station: payload.station ?? station,
      };
      pending.current = journal;
      pendingBody.current = payload;
      try {
        if (storageKey.current)
          sessionStorage.setItem(storageKey.current, JSON.stringify(journal));
      } catch {}
      const response =
        mode === "admission"
          ? (
              await apiClient.POST(
                "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/admissions/check-in",
                {
                  params: { path },
                  body: payload as S["AdmissionRequest"],
                  headers: { "Idempotency-Key": journal.key },
                  signal: AbortSignal.timeout(10000),
                },
              )
            ).data!
          : (
              await apiClient.POST(
                "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/redemptions",
                {
                  params: { path },
                  body: payload as S["RedemptionRequest"],
                  headers: { "Idempotency-Key": journal.key },
                  signal: AbortSignal.timeout(10000),
                },
              )
            ).data!;
      if (!mounted.current) return;
      setView(response);
      setQuantities({});
      setToken("");
      clearJournal();
      tone(true);
    } catch (e) {
      if (!mounted.current) return;
      if (
        e instanceof ApiError &&
        e.status < 500 &&
        ![401, 403, 429].includes(e.status) &&
        e.code !== "IDEMPOTENCY_CONFLICT"
      ) {
        clearJournal();
        setError(message(e));
      } else {
        setUnknown(true);
        setError(
          "Respuesta sin confirmar. No autorices ingreso ni repitas la entrega. Reintenta la misma confirmación o consulta el resultado.",
        );
      }
      tone(false);
    } finally {
      inFlight.current = false;
      if (mounted.current) setBusy(false);
    }
  }
  const success = view?.code === "ADMITTED" || view?.code === "REDEEMED";
  const chosen = Object.values(quantities).some((q) => q > 0);
  const validQuantities = Object.entries(quantities).every(
    ([id, q]) =>
      Number.isInteger(q) &&
      q >= 0 &&
      q <= (view?.lines?.find((l) => l.id === id)?.remaining ?? 0),
  );
  return (
    <div>
      <p className={"operation-network " + (online ? "" : "offline")}>
        {online
          ? "Conexión del navegador disponible; cada operación requiere respuesta del servidor."
          : "Sin conexión. No se aprueban ingresos ni entregas."}
      </p>
      <label>
        Estación
        <input
          value={station}
          onChange={(e) => setStation(e.target.value)}
          maxLength={80}
          disabled={busy || unknown}
        />
      </label>
      <label className="verification-check">
        <input
          type="checkbox"
          checked={sound}
          onChange={(e) => {
            setSound(e.target.checked);
            if (e.target.checked) armSound();
          }}
        />
        Avisos sonoros (el resultado también se muestra por escrito)
      </label>
      <div className="actions">
        <button
          type="button"
          disabled={busy || camera || unknown}
          onClick={() => void startCamera()}
        >
          Usar cámara
        </button>
        {camera && (
          <button type="button" className="secondary" onClick={stopCamera}>
            Detener cámara
          </button>
        )}
      </div>
      <video
        ref={video}
        className="scan-video"
        hidden={!camera}
        muted
        playsInline
        aria-label="Vista de cámara para leer QR"
      />
      {camera && (
        <p>
          Apunta al QR. La lectura solo consultará su estado; después deberás
          confirmar.
        </p>
      )}
      {cameraError && <p role="alert">{cameraError}</p>}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void inspect();
        }}
      >
        <label htmlFor={inputId}>
          {mode === "admission"
            ? "Código del pase individual"
            : "Código del vale de consumo"}
        </label>
          <textarea
            id={inputId}
            value={token}
            onChange={(e) => {
              setToken(e.target.value);
              setView(null);
              setError("");
              setNote("");
            }}
            maxLength={200}
            autoComplete="off"
            spellCheck={false}
            disabled={busy}
          />
        <button disabled={busy || !token.trim()}>
          {busy ? "Consultando servidor…" : "Consultar código"}
        </button>
      </form>
      {unknown && (
        <section className="pending-operation">
          <strong>Confirmación pendiente de recuperar</strong>
          <p>
            El servidor podría haberla registrado. Conservamos la clave del
            intento; no hacemos otra entrega automáticamente.
          </p>
          <div className="actions">
            <button disabled={busy} onClick={() => void recover()}>
              Consultar resultado
            </button>
            {pendingBody.current && (
              <button
                disabled={busy}
                onClick={() => void confirm(pendingBody.current!)}
              >
                Reintentar misma confirmación
              </button>
            )}
          </div>
        </section>
      )}
      {error && (
        <p role="alert" className="operation-result denied">
          {error}
        </p>
      )}
      {view && (
        <section
          aria-live="polite"
          role="status"
          className={
            "operation-result " +
            (success ? "approved" : view.canConfirm ? "ready" : "denied")
          }
        >
          <h3>{view.message}</h3>
          {view.zone && (
            <p>
              Zona: {view.zone} · Pase {view.ordinal}
            </p>
          )}
          {view.recordedAt && (
            <p>Registrado: {date(view.recordedAt)} (Lima).</p>
          )}
          {view.lines && (
            <div className="scan-balances">
              {view.lines.map((line) => (
                <div className="balance-line" key={line.id}>
                  <strong>{line.name}</strong>
                  <span>
                    {line.remaining} pendientes de {line.granted} ·{" "}
                    {line.delivered} entregados
                  </span>
                  {view.canConfirm && (
                    <label>
                      Entregar {line.name}
                      <input
                        type="number"
                        min="0"
                        max={line.remaining}
                        step="1"
                        value={quantities[line.id] ?? 0}
                        onChange={(e) =>
                          setQuantities((q) => ({
                            ...q,
                            [line.id]: Number(e.target.value),
                          }))
                        }
                        disabled={busy || line.remaining === 0}
                      />
                    </label>
                  )}
                </div>
              ))}
            </div>
          )}
          {view.canConfirm && (
            <>
              <p>
                {mode === "admission"
                  ? "Todavía no se registró ingreso."
                  : "Todavía no se descontaron las cantidades seleccionadas."}
              </p>
              <button
                disabled={
                  busy ||
                  !online ||
                  (mode === "redemption" && (!chosen || !validQuantities))
                }
                onClick={() => void confirm()}
              >
                {mode === "admission"
                  ? "Confirmar ingreso"
                  : "Confirmar entrega seleccionada"}
              </button>
            </>
          )}
        </section>
      )}
      {note && <p role="status">{note}</p>}
      {!unknown && (
        <button
          className="secondary"
          disabled={busy}
          onClick={() => {
            stopCamera();
            setToken("");
            setView(null);
            setError("");
            setNote("");
            setQuantities({});
          }}
        >
          Leer siguiente código
        </button>
      )}
    </div>
  );
}
