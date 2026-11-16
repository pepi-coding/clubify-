import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { apiClient } from "./api/client";
import type { components } from "./api/schema";
type S = components["schemas"];
const money = (v: number) =>
  new Intl.NumberFormat("es-PE", { style: "currency", currency: "PEN" }).format(
    v / 100,
  );
const date = (v: string) =>
  new Intl.DateTimeFormat("es-PE", {
    dateStyle: "short",
    timeStyle: "short",
    timeZone: "America/Lima",
  }).format(new Date(v));
const names: Record<string, string> = {
  PendingPayment: "Pendiente de verificación",
  Confirmed: "Reserva confirmada",
  Expired: "Reserva vencida",
  Cancelled: "Cancelada",
  PaymentReview: "Pago en revisión",
};
export function ManualPaymentInstructions({
  payment,
  status,
}: {
  payment: S["ManualPaymentInfo"];
  status: string;
}) {
  const [now, setNow] = useState(Date.now());
  const offset = useRef(new Date(payment.serverTime).getTime() - Date.now());
  useEffect(() => {
    offset.current = new Date(payment.serverTime).getTime() - Date.now();
  }, [payment.serverTime]);
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);
  const remaining = Math.max(
    0,
    Math.floor(
      (new Date(payment.expiresAt).getTime() - now - offset.current) / 1000,
    ),
  );
  return (
    <section className="card">
      <h2>Pago por Yape o Plin</h2>
      <p>
        Referencia de reserva:{" "}
        <strong className="reservation-reference">{payment.reference}</strong>
      </p>
      <p>{payment.instructions}</p>
      {status === "PendingPayment" && remaining > 0 ? (
        <>
          <p>
            Tiempo de reserva:{" "}
            <strong>
              {Math.floor(remaining / 60)}:
              {String(remaining % 60).padStart(2, "0")}
            </strong>{" "}
            restantes. Vence el {date(payment.expiresAt)} (Lima).
          </p>
          <p>
            Envía la captura del pago al WhatsApp de la sede. Tu reserva y tus
            pases se confirmarán cuando el promotor verifique el abono.
          </p>
          {payment.whatsAppUrl ? (
            <a className="primary" href={payment.whatsAppUrl}>
              Abrir WhatsApp y enviar comprobante
            </a>
          ) : (
            <p role="alert">
              La sede aún no configuró su WhatsApp. Contacta al administrador.
            </p>
          )}
        </>
      ) : status === "Confirmed" ? (
        <p>
          Pago verificado. Tu reserva está confirmada y tus pases están
          disponibles abajo.
        </p>
      ) : (
        <p>
          La reserva ya no está activa. No hagas un pago nuevo por esta reserva.
          Si ya pagaste, contacta a la sede para revisión.
        </p>
      )}
    </section>
  );
}
export function PaymentVerificationWorkspace({
  tenantId,
  venueId,
  admin,
}: {
  tenantId: string;
  venueId: string;
  admin: boolean;
}) {
  const path = { tenantId, venueId };
  const [eventId, setEventId] = useState("");
  const events = useQuery({
    queryKey: ["verification-events", tenantId, venueId],
    queryFn: async () =>
      (
        await apiClient.GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/events",
          { params: { path } },
        )
      ).data!,
  });
  return (
    <section className="card">
      <p className="eyebrow">PROMOTOR · VERIFICACIÓN MANUAL</p>
      <h2>Reservas y pagos por WhatsApp</h2>
      {admin && <WhatsAppSettings tenantId={tenantId} venueId={venueId} />}
      <label>
        Evento para verificar
        <select value={eventId} onChange={(e) => setEventId(e.target.value)}>
          <option value="">Selecciona un evento publicado</option>
          {events.data
            ?.filter((e) => e.status === "Published")
            .map((e) => (
              <option key={e.id} value={e.id}>
                {e.name}
              </option>
            ))}
        </select>
      </label>
      {events.isError && <p role="alert">{events.error.message}</p>}
      {eventId && (
        <PaymentQueue
          key={eventId}
          tenantId={tenantId}
          venueId={venueId}
          eventId={eventId}
        />
      )}
    </section>
  );
}
function WhatsAppSettings({
  tenantId,
  venueId,
}: {
  tenantId: string;
  venueId: string;
}) {
  const path = { tenantId, venueId };
  const [error, setError] = useState(""),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["payment-settings", tenantId, venueId],
    queryFn: async () =>
      (
        await apiClient.GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/payment-settings",
          { params: { path } },
        )
      ).data!,
  });
  return (
    <details className="card">
      <summary>Configurar WhatsApp de pagos</summary>
      {q.isPending ? (
        <p>Cargando configuración…</p>
      ) : q.isError ? (
        <p role="alert">{q.error.message}</p>
      ) : (
        <form
          key={q.data.whatsAppNumber}
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            setBusy(true);
            setError("");
            setMessage("");
            try {
              await apiClient.PUT(
                "/api/v1/tenants/{tenantId}/venues/{venueId}/payment-settings",
                {
                  params: { path },
                  body: {
                    whatsAppNumber: String(f.get("number")),
                    instructions: String(f.get("instructions")),
                  },
                },
              );
              await q.refetch();
              setMessage("WhatsApp de pagos guardado.");
            } catch (e) {
              setError((e as Error).message);
            } finally {
              setBusy(false);
            }
          }}
        >
          <label>
            WhatsApp de pagos (con código de país)
            <input
              name="number"
              type="tel"
              defaultValue={q.data.whatsAppNumber}
              placeholder="+51 …"
              maxLength={25}
            />
          </label>
          <label>
            Instrucciones de Yape o Plin
            <textarea
              name="instructions"
              defaultValue={q.data.instructions}
              maxLength={1000}
              required
            />
          </label>
          <p>
            Deja el número vacío para suspender nuevas reservas por WhatsApp.
          </p>
          <button disabled={busy}>Guardar WhatsApp de pagos</button>
        </form>
      )}
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
    </details>
  );
}
function PaymentQueue({
  tenantId,
  venueId,
  eventId,
}: {
  tenantId: string;
  venueId: string;
  eventId: string;
}) {
  const path = { tenantId, venueId, eventId };
  const [filter, setFilter] = useState("pending");
  const [search, setSearch] = useState("");
  const [feedback, setFeedback] = useState("");
  const q = useQuery({
    queryKey: ["payment-queue", tenantId, venueId, eventId],
    queryFn: async () =>
      (
        await apiClient.GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/manual-payments",
          { params: { path } },
        )
      ).data!,
    refetchInterval: 10000,
  });
  return (
    <section>
      <h3>Verificar comprobantes</h3>
      <p>
        Revisa el abono en tu cuenta de Yape o Plin y compara referencia e
        importe. Una captura por sí sola no confirma el pago.
      </p>
      <label>
        Buscar referencia o cliente
        <input value={search} onChange={(e) => setSearch(e.target.value)} />
      </label>
      <label>
        Estado de reservas
        <select value={filter} onChange={(e) => setFilter(e.target.value)}>
          <option value="pending">Pendientes</option>
          <option value="all">Todas (últimas 100)</option>
        </select>
      </label>
      <button className="secondary" onClick={() => void q.refetch()}>
        Actualizar reservas
      </button>
      {feedback && <p role="status">{feedback}</p>}
      {q.isPending ? (
        <p>Cargando reservas…</p>
      ) : q.isError ? (
        <p role="alert">{q.error.message}</p>
      ) : (
        <>
          {q.data
            .filter(
              (o) =>
                (filter === "all" || o.status === "PendingPayment") &&
                `${o.reference} ${o.customerEmail}`
                  .toLowerCase()
                  .includes(search.toLowerCase()),
            )
            .map((o) => (
              <VerifyReservation
                key={o.id}
                order={o}
                path={path}
                refresh={async () => {
                  setFeedback("Verificación guardada. Consulta Todas para ver el resultado.");
                  await q.refetch();
                }}
              />
            ))}
          {!q.data.length && (
            <p>No hay reservas por WhatsApp para este evento.</p>
          )}
        </>
      )}
    </section>
  );
}
function VerifyReservation({
  order: o,
  path,
  refresh,
}: {
  order: S["PaymentQueueItem"];
  path: { tenantId: string; venueId: string; eventId: string };
  refresh: () => Promise<void>;
}) {
  const keys = useRef(new Map<string, string>());
  const [error, setError] = useState(""),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  return (
    <article className="card">
      <h3>Reserva {o.reference}</h3>
      <p>
        {o.customerEmail} · <strong>{money(o.totalCents)}</strong> ·{" "}
        {names[o.status]}
      </p>
      <p>Vence: {date(o.expiresAt)} (Lima).</p>
      {o.lines.map((l, i) => (
        <p key={i}>
          {l.name} × {l.quantity} · {l.admissions * l.quantity} accesos
        </p>
      ))}
      {o.verifications.map((v, i) => (
        <p key={i}>
          {v.method}:{" "}
          {v.status === "Succeeded"
            ? "Abono verificado"
            : "Comprobante rechazado"}
          {v.receiptReference ? ` · Operación ${v.receiptReference}` : ""} ·{" "}
          {date(v.verifiedAt)}
        </p>
      ))}
      {["PendingPayment", "Expired"].includes(o.status) && (
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            const approved = f.get("decision") === "approve";
            const paid = String(f.get("paidAt"));
            const body = {
              approved,
              method: String(f.get("method")),
              receiptReference: String(f.get("reference")),
              amountCents: Math.round(Number(f.get("amount")) * 100),
              paidAt: paid ? new Date(paid + "-05:00").toISOString() : null,
              note: String(f.get("note")),
            };
            const fingerprint = JSON.stringify(body);
            let key = keys.current.get(fingerprint);
            if (!key) {
              key = crypto.randomUUID();
              keys.current.set(fingerprint, key);
            }
            setBusy(true);
            setError("");
            setMessage("");
            try {
              const r = await apiClient.POST(
                "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/manual-payments/{orderId}/verify",
                {
                  params: { path: { ...path, orderId: o.id } },
                  body,
                  headers: { "Idempotency-Key": key },
                },
              );
              setMessage(
                r.data!.status === "Confirmed"
                  ? "Pago verificado y reserva confirmada."
                  : r.data!.status === "PaymentReview"
                    ? "Pago registrado en revisión: reserva vencida, sin pases."
                    : "Comprobante rechazado. Puede corregirse mientras la reserva siga vigente.",
              );
              await refresh();
            } catch (e) {
              setError((e as Error).message);
            } finally {
              setBusy(false);
            }
          }}
        >
          <label>
            Método de pago
            <select name="method">
              <option>Yape</option>
              <option>Plin</option>
            </select>
          </label>
          <label>
            Código de operación
            <input name="reference" maxLength={80} />
          </label>
          <label>
            Importe recibido en soles
            <input
              name="amount"
              type="number"
              min="0"
              step="0.01"
              defaultValue={o.totalCents / 100}
            />
          </label>
          <label>
            Fecha y hora del abono (Lima)
            <input name="paidAt" type="datetime-local" />
          </label>
          <label>
            Nota de verificación
            <textarea name="note" maxLength={500} required />
          </label>
          <label>
            Decisión
            <select name="decision">
              <option value="approve">Confirmar abono recibido</option>
              <option value="reject">Rechazar comprobante</option>
            </select>
          </label>
          <label className="verification-check">
            <input type="checkbox" required />
            Revisé la operación en Yape o Plin y los datos del comprobante.
          </label>
          {o.status === "Expired" && (
            <p>
              La reserva venció. Un abono se registrará en revisión y no emitirá
              pases.
            </p>
          )}
          <button disabled={busy}>Guardar verificación de pago</button>
        </form>
      )}
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
    </article>
  );
}
