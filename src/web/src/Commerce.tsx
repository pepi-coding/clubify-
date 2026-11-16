import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { QRCodeSVG } from "qrcode.react";
import { apiClient } from "./api/client";
import { ManualPaymentInstructions } from "./ManualPayments";
import type { components } from "./api/schema";
type S = components["schemas"];
type Catalog = S["CatalogDto"];
type Offer = S["OfferDto"];
const money = (v: number) =>
  new Intl.NumberFormat("es-PE", { style: "currency", currency: "PEN" }).format(
    v / 100,
  );
const states: Record<string, string> = {
  Active: "Reservado",
  Confirmed: "Confirmado",
  PendingPayment: "Pendiente de verificación",
  Expired: "Vencido",
  Cancelled: "Cancelado",
  PaymentReview: "Pago en revisión",
  Available: "Disponible",
  Reserved: "Reservado",
  Sold: "Vendido",
  Blocked: "Bloqueado",
};
// Keep the key for an identical retry after a lost response. A changed body gets a fresh key.
function useOperation() {
  const keys = useRef(new Map<string, string>());
  return (op: string, body: unknown) => {
    const fingerprint = op + JSON.stringify(body);
    let key = keys.current.get(fingerprint);
    if (!key) {
      key = crypto.randomUUID();
      keys.current.set(fingerprint, key);
    }
    return { "Idempotency-Key": key };
  };
}
export function useCatalog(eventId: string) {
  return useQuery({
    queryKey: ["catalog", eventId],
    queryFn: async () =>
      (
        await apiClient.GET("/api/v1/public/events/{eventId}/catalog", {
          params: { path: { eventId } },
        })
      ).data!,
    refetchInterval: 10000,
  });
}
export function EventShop({ eventId }: { eventId: string }) {
  const q = useCatalog(eventId);
  const nav = useNavigate();
  const key = useOperation();
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  if (q.isPending) return <p>Cargando disponibilidad…</p>;
  if (q.isError) return <p role="alert">{q.error.message}</p>;
  const cat = q.data;
  return (
    <section className="card">
      <p className="eyebrow">
        {cat.paymentMode === "WhatsAppManual"
          ? "RESERVAS · YAPE O PLIN POR WHATSAPP"
          : "RESERVAS · PAGO SIMULADO"}
      </p>
      <h2>Ofertas y disponibilidad</h2>
      <p>
        Reserva durante {cat.reservationMinutes} minutos.{" "}
        {cat.paymentMode === "WhatsAppManual"
          ? "Te llevaremos a WhatsApp para coordinar tu pago y enviar el comprobante. El promotor confirmará el abono."
          : "El servidor confirma precio y cupo al reservar."}
      </p>
      {!cat.paymentsConfigured && (
        <p role="alert">
          La sede aún no configuró su WhatsApp de pagos. Las reservas estarán
          disponibles cuando lo configure.
        </p>
      )}
      <div className="inventory-list">
        {cat.inventory.map((i) => (
          <p key={i.id}>
            <strong>{i.name}</strong> · {states[i.state]} · {i.available}{" "}
            disponibles · {i.held} reservados · {i.sold} vendidos
          </p>
        ))}
      </div>
      <div className="grid">
        {cat.offers.map((o) => (
          <OfferCard
            key={o.id}
            o={o}
            cat={cat}
            busy={busy || !cat.paymentsConfigured}
            reserve={async (quantity) => {
              setBusy(true);
              setError("");
              const body = {
                eventId,
                lines: [{ offerVersionId: o.id, quantity }],
              };
              try {
                if (cat.paymentMode === "WhatsAppManual") {
                  const r = await apiClient.POST("/api/v1/reservations", {
                    body,
                    headers: key("reservation", body),
                  });
                  nav(`/orders/${r.data!.orderId}`);
                  if (r.data!.payment.whatsAppUrl)
                    window.location.assign(r.data!.payment.whatsAppUrl);
                  return;
                }
                const r = await apiClient.POST("/api/v1/holds", {
                  body,
                  headers: key("hold", body),
                });
                nav(`/checkout/${r.data!.id}`);
              } catch (e) {
                setError((e as Error).message);
                await q.refetch();
              } finally {
                setBusy(false);
              }
            }}
          />
        ))}
      </div>
      {!cat.offers.length && <p>Este evento todavía no tiene ofertas.</p>}
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
function OfferCard({
  o,
  cat,
  busy,
  reserve,
}: {
  o: Offer;
  cat: Catalog;
  busy: boolean;
  reserve: (q: number) => Promise<void>;
}) {
  const [quantity, setQuantity] = useState(1);
  const inv = cat.inventory.find((i) => i.resourceId === o.resourceId);
  const global = cat.inventory.find((i) => !i.resourceId);
  const max = Math.min(
    100,
    o.resourceUnits ? Math.floor((inv?.available ?? 0) / o.resourceUnits) : 100,
    o.admissions ? Math.floor((global?.available ?? 0) / o.admissions) : 100,
  );
  return (
    <article className="card">
      <h3>{o.name}</h3>
      <p className="price">{money(o.priceCents)}</p>
      <p>{o.admissions} accesos por unidad</p>
      {o.drinks.map((d) => (
        <p key={d.productId}>
          {d.name} × {d.quantity}
        </p>
      ))}
      <label>
        Cantidad de {o.name}
        <input
          type="number"
          min="1"
          max={Math.max(1, max)}
          value={quantity}
          onChange={(e) => setQuantity(Number(e.target.value))}
        />
      </label>
      <p>Total de selección: {money(o.priceCents * quantity)}</p>
      <button
        disabled={
          busy || max < quantity || quantity < 1 || !Number.isInteger(quantity)
        }
        onClick={() => void reserve(quantity)}
      >
        Reservar {o.name}
      </button>
    </article>
  );
}
export function Checkout() {
  const { holdId = "" } = useParams();
  const nav = useNavigate();
  const key = useOperation();
  const [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [tick, setTick] = useState(Date.now());
  const q = useQuery({
    queryKey: ["hold", holdId],
    queryFn: async () =>
      (
        await apiClient.GET("/api/v1/me/holds/{id}", {
          params: { path: { id: holdId } },
        })
      ).data!,
    refetchInterval: 5000,
  });
  useEffect(() => {
    const id = setInterval(() => setTick(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);
  const [offset, setOffset] = useState(0);
  useEffect(() => {
    if (q.data) setOffset(new Date(q.data.serverTime).getTime() - Date.now());
  }, [q.data]);
  if (q.isPending) return <p>Cargando reserva…</p>;
  if (q.isError) return <p role="alert">{q.error.message}</p>;
  const h = q.data;
  const remaining = Math.max(
    0,
    Math.floor((new Date(h.expiresAt).getTime() - tick - offset) / 1000),
  );
  return (
    <section className="card">
      <h1>Tu reserva</h1>
      <p>
        {states[h.status]} · {Math.floor(remaining / 60)}:
        {String(remaining % 60).padStart(2, "0")} restantes
      </p>
      <p>
        Total: <strong>{money(h.totalCents)}</strong>
      </p>
      <p>Checkout de demostración. No se cobrará dinero real.</p>
      <button
        disabled={busy || h.status !== "Active" || remaining === 0}
        onClick={async () => {
          setBusy(true);
          setError("");
          const body = { holdId };
          try {
            const r = await apiClient.POST("/api/v1/orders", {
              body,
              headers: key("order", body),
            });
            nav(`/orders/${r.data!.id}`);
          } catch (e) {
            setError((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        Continuar al pago demo
      </button>
      <button
        className="secondary"
        disabled={busy || h.status !== "Active"}
        onClick={async () => {
          setBusy(true);
          try {
            await apiClient.POST("/api/v1/me/holds/{id}/cancel", {
              params: { path: { id: holdId } },
              headers: key("cancel", holdId),
            });
            await q.refetch();
          } catch (e) {
            setError((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        Cancelar reserva
      </button>
      <p>
        <Link to={`/events/${h.eventId}`}>Volver al evento</Link>
      </p>
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
export function MyOrders() {
  const q = useQuery({
    queryKey: ["orders"],
    queryFn: async () => (await apiClient.GET("/api/v1/me/orders")).data!,
  });
  return (
    <section>
      <h1>Mis pedidos</h1>
      {q.isPending ? (
        <p>Cargando pedidos…</p>
      ) : q.isError ? (
        <p role="alert">{q.error.message}</p>
      ) : q.data.length ? (
        <div className="grid">
          {q.data.map((o) => (
            <Link className="card" key={o.id} to={`/orders/${o.id}`}>
              <h2>{states[o.status]}</h2>
              <p>{money(o.totalCents)}</p>
              <small>Reserva {o.reference}</small>
            </Link>
          ))}
        </div>
      ) : (
        <p>Aún no tienes pedidos.</p>
      )}
    </section>
  );
}
export function OrderDetail() {
  const { orderId = "" } = useParams();
  const key = useOperation();
  const [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["order", orderId],
    refetchInterval: 10000,
    queryFn: async () =>
      (
        await apiClient.GET("/api/v1/me/orders/{id}", {
          params: { path: { id: orderId } },
        })
      ).data!,
  });
  if (q.isPending) return <p>Cargando pedido…</p>;
  if (q.isError) return <p role="alert">{q.error.message}</p>;
  const o = q.data;
  return (
    <section>
      <Link to="/orders">← Mis pedidos</Link>
      <p className="eyebrow">
        {o.source === "WhatsAppManual"
          ? "RESERVA · VERIFICACIÓN DE PAGO POR WHATSAPP"
          : "DEMOSTRACIÓN · SIN COBRO REAL"}
      </p>
      <h1>{o.eventName}</h1>
      <p>Reserva <strong className="reservation-reference">{o.reference}</strong></p>
      <h2>{states[o.status]}</h2>
      <p>
        {money(o.totalCents)} ·{" "}
        {o.source === "Complimentary"
          ? "Cortesía"
          : o.source === "CashDemo"
            ? "Caja demo"
            : o.source === "WhatsAppManual"
              ? "Yape / Plin · verificación manual"
              : "Compra online demo"}
      </p>
      {o.lines.map((l, i) => (
        <article className="card" key={i}>
          <h3>
            {l.name} × {l.quantity}
          </h3>
          <p>
            {l.admissions * l.quantity} accesos · {money(l.unitPriceCents)} por
            unidad
          </p>
          {l.drinks.map((d) => (
            <p key={d.productId}>
              {d.name} × {d.quantity * l.quantity}
            </p>
          ))}
        </article>
      ))}
      {o.payment && (
        <ManualPaymentInstructions payment={o.payment} status={o.status} />
      )}
      {!o.payment && ["PendingPayment", "Expired"].includes(o.status) && (
        <section className="card">
          <h2>Pago simulado</h2>
          <p>
            Elige un resultado de prueba. Un pago vencido queda en revisión, sin
            emitir accesos.
          </p>
          <div className="actions">
            {[true, false].map((approved) => (
              <button
                key={String(approved)}
                disabled={busy}
                onClick={async () => {
                  setBusy(true);
                  setError("");
                  const body = { approved };
                  try {
                    await apiClient.POST(
                      "/api/v1/demo/orders/{id}/payment-result",
                      {
                        params: { path: { id: orderId } },
                        body,
                        headers: key("payment:" + orderId, body),
                      },
                    );
                    await q.refetch();
                  } catch (e) {
                    setError((e as Error).message);
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                {approved ? "Simular pago aprobado" : "Simular pago rechazado"}
              </button>
            ))}
          </div>
        </section>
      )}
      {o.paymentStatuses.includes("Failed") && (
        <p>Hubo un intento rechazado. Ese intento no emitió beneficios.</p>
      )}
      {o.status === "PaymentReview" && (
        <p role="alert">
          Pago registrado para revisión. La reserva venció y no se emitieron
          accesos ni consumos.
        </p>
      )}
      {o.tickets.length > 0 && (
        <>
          <h2>Pases individuales ({o.tickets.length})</h2>
          <p>
            Cada pase admite un uso. Compartir el QR comparte la credencial; no
            transfiere titularidad.
          </p>
          <div className="grid">
            {o.tickets.map((t, i) => (
              <article className="card qr-card" key={t.id}>
                <h3>
                  Pase {i + 1} · {t.zone}
                </h3>
                <p>
                  {t.status === "Used"
                    ? "Ingreso registrado · sin reingreso"
                    : t.status === "Revoked"
                      ? "Pase revocado"
                      : "Sin ingreso registrado"}
                </p>
                <QRCodeSVG
                  value={t.token}
                  size={180}
                  marginSize={4}
                  title={`Pase ${i + 1}`}
                />
                <details>
                  <summary>Mostrar código del pase {i + 1}</summary>
                  <code>{t.token}</code>
                </details>
              </article>
            ))}
          </div>
        </>
      )}
      {o.voucher && (
        <section className="card qr-card">
          <h2>Vale de consumo independiente</h2>
          <p>
            Este QR no sirve como entrada. El canje se habilita después de un
            ingreso del pedido. Preséntalo en barra para elegir las cantidades a
            entregar.
          </p>
          <QRCodeSVG
            value={o.voucher.token}
            size={180}
            marginSize={4}
            title="Vale de consumo"
          />
          {o.voucher.lines.map((l) => (
            <p key={l.name}>
              {l.name}: {l.granted - l.delivered} pendientes de {l.granted}
            </p>
          ))}
          <details>
            <summary>Mostrar código del vale</summary>
            <code>{o.voucher.token}</code>
          </details>
        </section>
      )}
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
export function CommerceWorkspace({
  tenantId,
  venueId,
  admin,
}: {
  tenantId: string;
  venueId: string;
  admin: boolean;
}) {
  const events = useQuery({
    queryKey: ["sale-events", tenantId, venueId],
    queryFn: async () =>
      (
        await apiClient.GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/events",
          { params: { path: { tenantId, venueId } } },
        )
      ).data!,
  });
  const [eventId, setEventId] = useState("");
  return (
    <section className="card">
      <h2>Ofertas, caja y cortesías</h2>
      {events.isError && <p role="alert">{events.error.message}</p>}
      <label>
        Evento de venta
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
      {eventId && (
        <EventCommerceAdmin
          key={eventId}
          tenantId={tenantId}
          venueId={venueId}
          eventId={eventId}
          admin={admin}
        />
      )}
    </section>
  );
}
function EventCommerceAdmin({
  tenantId,
  venueId,
  eventId,
  admin,
}: {
  tenantId: string;
  venueId: string;
  eventId: string;
  admin: boolean;
}) {
  const q = useCatalog(eventId);
  const key = useOperation();
  const path = { tenantId, venueId, eventId };
  const [error, setError] = useState(""),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [edit, setEdit] = useState<Offer | undefined>();
  const event = useQuery({
    queryKey: ["sale-event", eventId],
    queryFn: async () =>
      (
        await apiClient.GET("/api/v1/public/events/{eventId}", {
          params: { path: { eventId } },
        })
      ).data!,
  });
  const products = useQuery({
    queryKey: ["products", tenantId, venueId],
    enabled: admin,
    queryFn: async () =>
      (
        await apiClient.GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/products",
          { params: { path: { tenantId, venueId } } },
        )
      ).data!,
  });
  const customers = useQuery({
    queryKey: ["sale-customers", eventId],
    queryFn: async () =>
      (
        await apiClient.GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/customers",
          { params: { path } },
        )
      ).data!,
  });
  async function act(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await action();
      await q.refetch();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  if (q.isPending || event.isPending) return <p>Cargando catálogo…</p>;
  if (q.isError || event.isError)
    return <p role="alert">{q.error?.message ?? event.error?.message}</p>;
  return (
    <>
      <p className="eyebrow">SOLO DEMOSTRACIÓN · IMPORTES SIMULADOS</p>
      {admin && (
        <>
          <h3>Inventario</h3>
          {q.data.inventory.map((i) => (
            <div className="list-row" key={i.id}>
              <span>
                {i.name} · {states[i.state]} · {i.available} libres · {i.held}{" "}
                reservados · {i.sold} vendidos
              </span>
              <button
                disabled={busy}
                onClick={() =>
                  void act(async () => {
                    await apiClient.POST(
                      "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/inventory/{inventoryId}/block",
                      {
                        params: { path: { ...path, inventoryId: i.id } },
                        body: { blocked: i.state !== "Blocked" },
                      },
                    );
                  })
                }
              >
                {i.state === "Blocked" ? "Desbloquear" : "Bloquear"}
              </button>
            </div>
          ))}
          <details>
            <summary>Productos de bebida</summary>
            {products.data?.map((p) => (
              <p key={p.id}>{p.name}</p>
            ))}
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const f = new FormData(e.currentTarget);
                void act(async () => {
                  await apiClient.POST(
                    "/api/v1/tenants/{tenantId}/venues/{venueId}/products",
                    {
                      params: { path: { tenantId, venueId } },
                      body: { name: String(f.get("name")) },
                    },
                  );
                  await products.refetch();
                  setMessage("Producto creado.");
                });
              }}
            >
              <label>
                Nombre del producto
                <input name="name" required maxLength={100} />
              </label>
              <button disabled={busy}>Crear producto</button>
            </form>
          </details>
          <h3>Ofertas publicadas</h3>
          {q.data.offers.map((o) => (
            <div className="list-row" key={o.id}>
              <span>
                {o.name} · v{o.number} · {money(o.priceCents)}
              </span>
              <button disabled={busy} onClick={() => setEdit(o)}>
                Editar oferta
              </button>
              <button
                disabled={busy}
                onClick={() =>
                  void act(async () => {
                    await apiClient.POST(
                      "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/offers/{offerId}/archive",
                      { params: { path: { ...path, offerId: o.offerId } } },
                    );
                    setMessage(
                      "Oferta archivada; pedidos y reservas conservados.",
                    );
                  })
                }
              >
                Archivar oferta
              </button>
            </div>
          ))}
          <OfferForm
            key={edit?.id ?? "new"}
            edit={edit}
            resources={event.data.resources}
            products={products.data ?? []}
            busy={busy}
            cancel={() => setEdit(undefined)}
            save={(body) =>
              act(async () => {
                await apiClient.POST(
                  "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/offers",
                  { params: { path }, body },
                );
                setEdit(undefined);
                setMessage("Versión de oferta publicada.");
              })
            }
          />
        </>
      )}
      <h3>
        {q.data.paymentMode === "WhatsAppManual"
          ? "Cortesías"
          : "Venta presencial demo"}
      </h3>
      {customers.isError && <p role="alert">{customers.error.message}</p>}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const f = new FormData(e.currentTarget);
          const body = {
            customerEmail: String(f.get("customer")),
            lines: [
              {
                offerVersionId: String(f.get("offer")),
                quantity: Number(f.get("quantity")),
              },
            ],
            reason: String(f.get("reason") ?? ""),
            approved: f.get("result") !== "false",
          };
          const free = f.get("source") === "free";
          void act(async () => {
            const r = free
              ? await apiClient.POST(
                  "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/complimentary-orders",
                  {
                    params: { path },
                    body,
                    headers: key("free:" + eventId, body),
                  },
                )
              : await apiClient.POST(
                  "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/cash-orders",
                  {
                    params: { path },
                    body,
                    headers: key("cash:" + eventId, body),
                  },
                );
            setMessage(
              `Pedido ${r.data!.reference}: ${states[r.data!.status]}. El cliente puede verlo en Mis pedidos.`,
            );
          });
        }}
      >
        <label>
          Cliente existente
          <select name="customer" required>
            {customers.data?.map((email) => (
              <option key={email}>{email}</option>
            ))}
          </select>
        </label>
        <label>
          Oferta
          <select name="offer" required>
            {q.data.offers.map((o) => (
              <option key={o.id} value={o.id}>
                {o.name} · {money(o.priceCents)}
              </option>
            ))}
          </select>
        </label>
        <label>
          Unidades
          <input
            name="quantity"
            type="number"
            min="1"
            max="100"
            defaultValue={1}
            required
          />
        </label>
        <label>
          Origen
          <select
            name="source"
            defaultValue={
              q.data.paymentMode === "WhatsAppManual" ? "free" : "cash"
            }
          >
            {q.data.paymentMode !== "WhatsAppManual" && (
              <option value="cash">Caja demo</option>
            )}
            {admin && <option value="free">Cortesía</option>}
          </select>
        </label>
        <label>
          Motivo de cortesía
          <input name="reason" maxLength={500} />
        </label>
        <label>
          Resultado simulado
          <select name="result">
            <option value="true">Aprobado</option>
            <option value="false">Rechazado</option>
          </select>
        </label>
        <button disabled={busy || !q.data.offers.length}>
          Registrar pedido demo
        </button>
      </form>
      {message && <p role="status">{message}</p>}
      {error && <p role="alert">{error}</p>}
    </>
  );
}
function OfferForm({
  edit,
  resources,
  products,
  busy,
  cancel,
  save,
}: {
  edit?: Offer;
  resources: S["ResourceResponse"][];
  products: S["Product"][];
  busy: boolean;
  cancel: () => void;
  save: (body: S["OfferSave"]) => Promise<void>;
}) {
  const [resourceId, setResourceId] = useState(edit?.resourceId ?? "");
  const resource = resources.find((r) => r.id === resourceId);
  return (
    <form
      className="card"
      onSubmit={(e) => {
        e.preventDefault();
        const f = new FormData(e.currentTarget);
        const units =
          resource?.mode === "Exclusive"
            ? 1
            : resource
              ? Number(f.get("units"))
              : 0;
        void save({
          offerId: edit?.offerId ?? null,
          expectedRevision: edit?.number ?? 0,
          name: String(f.get("name")),
          priceCents: Math.round(Number(f.get("price")) * 100),
          resourceId: resourceId || null,
          resourceUnits: units,
          admissions:
            resource?.mode === "Exclusive" ? resource.humanCapacity : units,
          drinks: products
            .map((p) => ({
              productId: p.id!,
              quantity: Number(f.get("drink:" + p.id)),
            }))
            .filter((d) => d.quantity > 0),
        });
      }}
    >
      <h3>{edit ? "Nueva versión de oferta" : "Crear oferta"}</h3>
      <label>
        Nombre de oferta
        <input
          name="name"
          defaultValue={edit?.name ?? ""}
          maxLength={120}
          required
        />
      </label>
      <label>
        Precio en soles
        <input
          name="price"
          type="number"
          min="0"
          max="1000000"
          step="0.01"
          defaultValue={(edit?.priceCents ?? 0) / 100}
          required
        />
      </label>
      <label>
        Recurso
        <select
          value={resourceId}
          onChange={(e) => setResourceId(e.target.value)}
        >
          <option value="">Solo bebidas · sin nuevos accesos</option>
          {resources.map((r) => (
            <option value={r.id} key={r.id}>
              {r.name} ·{" "}
              {r.mode === "Exclusive"
                ? `${r.humanCapacity} accesos por box`
                : "por cupo"}
            </option>
          ))}
        </select>
      </label>
      {resource?.mode === "Capacity" && (
        <label>
          Accesos por unidad
          <input
            name="units"
            type="number"
            min="1"
            max="1000"
            defaultValue={edit?.resourceUnits ?? 1}
            required
          />
        </label>
      )}
      <p>Bebidas incluidas por unidad de oferta:</p>
      {products.map((p) => (
        <label key={p.id}>
          {p.name}
          <input
            name={"drink:" + p.id}
            type="number"
            min="0"
            max="100"
            defaultValue={
              edit?.drinks.find((d) => d.productId === p.id)?.quantity ?? 0
            }
          />
        </label>
      ))}
      <button disabled={busy}>Publicar oferta</button>
      {edit && (
        <button type="button" className="secondary" onClick={cancel}>
          Cancelar edición
        </button>
      )}
    </form>
  );
}
