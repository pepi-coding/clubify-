import { NochePhoto, limaDay } from "./Noche";
import { EventShop, useCatalog } from "./Commerce";
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "./api/client";
import type { components } from "./api/schema";
import {
  MapCanvas,
  MapList,
  ElementProperties,
  palette,
  type MapElement,
} from "./MapView";
type Event = components["schemas"]["EventResponse"];
type Layout = components["schemas"]["LayoutResponse"];
type Version = components["schemas"]["VersionResponse"];
type Membership = components["schemas"]["MembershipResponse"];
const scope = (tenantId: string, venueId: string) => ({
  params: { path: { tenantId, venueId } },
});
const dateText = (value: string) =>
  new Intl.DateTimeFormat("es-PE", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "America/Lima",
  }).format(new Date(value));
function localTime(value: string) {
  const p = new Intl.DateTimeFormat("sv-SE", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZone: "America/Lima",
  }).formatToParts(new Date(value));
  const v = (type: string) => p.find((x) => x.type === type)!.value;
  return `${v("year")}-${v("month")}-${v("day")}T${v("hour")}:${v("minute")}`;
}
export function VenueCreation({ memberships }: { memberships: Membership[] }) {
  const admins = memberships.filter((x) => x.role === "TenantAdmin");
  const cache = useQueryClient();
  const [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  if (!admins.length) return null;
  return (
    <details className="card">
      <summary>Crear sede</summary>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          const form = e.currentTarget;
          const f = new FormData(form);
          setBusy(true);
          setMessage("");
          try {
            await apiClient.POST("/api/v1/tenants/{tenantId}/venues", {
              params: { path: { tenantId: String(f.get("tenant")) } },
              body: {
                name: String(f.get("name")),
                capacity: Number(f.get("capacity")),
              },
            });
            await cache.invalidateQueries({ queryKey: ["me"] });
            form.reset();
            setMessage("Sede creada. Selecciónala en la lista de sedes.");
          } catch (e) {
            setMessage((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <label>
          Empresa
          <select name="tenant">
            {admins.map((x) => (
              <option key={x.tenantId} value={x.tenantId}>
                {x.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Nombre de nueva sede
          <input name="name" required maxLength={160} />
        </label>
        <label>
          Aforo de nueva sede
          <input
            name="capacity"
            type="number"
            min={1}
            max={100000}
            defaultValue={100}
            required
          />
        </label>
        <button disabled={busy}>Crear sede</button>
        <p role="status">{message}</p>
      </form>
    </details>
  );
}
export function PlanningWorkspace({
  tenantId,
  venueId,
  admin,
  tenantAdmin,
}: {
  tenantId: string;
  venueId: string;
  admin: boolean;
  tenantAdmin: boolean;
}) {
  const cache = useQueryClient(),
    navigate = useNavigate();
  const [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [editing, setEditing] = useState<Event | null | undefined>();
  const layouts = useQuery({
    queryKey: ["layouts", tenantId, venueId],
    queryFn: () =>
      apiClient
        .GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/layouts",
          scope(tenantId, venueId),
        )
        .then((r) => r.data!),
    enabled: admin,
  });
  const versions = useQuery({
    queryKey: ["versions", tenantId, venueId],
    queryFn: () =>
      apiClient
        .GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/versions",
          scope(tenantId, venueId),
        )
        .then((r) => r.data!),
    enabled: admin,
  });
  const events = useQuery({
    queryKey: ["events", tenantId, venueId],
    queryFn: () =>
      apiClient
        .GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/events",
          scope(tenantId, venueId),
        )
        .then((r) => r.data!),
  });
  async function refresh() {
    await cache.invalidateQueries({ queryKey: ["events", tenantId, venueId] });
    await cache.invalidateQueries({ queryKey: ["public-events"] });
  }
  async function createLayout(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    setBusy(true);
    setError("");
    try {
      const r = await apiClient.POST(
        "/api/v1/tenants/{tenantId}/venues/{venueId}/layouts",
        { ...scope(tenantId, venueId), body: { name: String(f.get("name")) } },
      );
      await cache.invalidateQueries({ queryKey: ["layouts"] });
      navigate(`/workspace/${tenantId}/${venueId}/layouts/${r.data!.id}`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      {admin && (
        <section className="card">
          <p className="eyebrow">DISTRIBUCIÓN</p>
          <h2>Mapas de la sede</h2>
          {layouts.isPending ? (
            <p>Cargando mapas…</p>
          ) : layouts.isError ? (
            <p role="alert">{layouts.error.message}</p>
          ) : layouts.data?.length ? (
            <div className="grid">
              {layouts.data.map((l) => (
                <Link
                  className="card"
                  key={l.id}
                  to={`/workspace/${tenantId}/${venueId}/layouts/${l.id}`}
                >
                  <h3>{l.name}</h3>
                  <span>Abrir editor →</span>
                </Link>
              ))}
            </div>
          ) : (
            <p>Aún no hay plantillas. Crea el primer mapa para esta sede.</p>
          )}
          <form onSubmit={createLayout}>
            <label>
              Nombre del mapa
              <input name="name" required maxLength={160} />
            </label>
            <button disabled={busy}>Crear mapa</button>
          </form>
        </section>
      )}
      <section className="card">
        <p className="eyebrow">PROGRAMACIÓN · HORA DE LIMA</p>
        <h2>Eventos de la sede</h2>
        {events.isPending ? (
          <p>Cargando eventos…</p>
        ) : events.isError ? (
          <p role="alert">{events.error.message}</p>
        ) : events.data?.length ? (
          events.data.map((item) => (
            <div className="event-row" key={item.id}>
              <div>
                <h3>{item.name}</h3>
                <p>
                  {dateText(item.startsAt)} → {dateText(item.endsAt)}
                </p>
                <span className="badge">
                  {item.status === "Draft" ? "Borrador" : "Publicado"} · Aforo{" "}
                  {item.capacity}
                </span>
              </div>
              <div className="actions">
                {item.status === "Published" ? (
                  <Link className="primary" to={`/events/${item.id}`}>
                    Ver mapa publicado
                  </Link>
                ) : (
                  admin && (
                    <>
                      <button onClick={() => setEditing(item)}>
                        Editar evento
                      </button>
                      <button
                        disabled={busy}
                        onClick={async () => {
                          setBusy(true);
                          setError("");
                          try {
                            await apiClient.POST(
                              "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}/publish",
                              {
                                params: {
                                  path: { tenantId, venueId, eventId: item.id },
                                },
                                body: { expectedRevision: item.revision },
                              },
                            );
                            await refresh();
                          } catch (e) {
                            setError((e as Error).message);
                          } finally {
                            setBusy(false);
                          }
                        }}
                      >
                        Publicar evento
                      </button>
                      <button
                        className="danger"
                        onClick={async () => {
                          if (!confirm("¿Eliminar este borrador de evento?"))
                            return;
                          try {
                            await apiClient.DELETE(
                              "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}",
                              {
                                params: {
                                  path: { tenantId, venueId, eventId: item.id },
                                },
                              },
                            );
                            await refresh();
                          } catch (e) {
                            setError((e as Error).message);
                          }
                        }}
                      >
                        Eliminar borrador
                      </button>
                    </>
                  )
                )}
              </div>
            </div>
          ))
        ) : (
          <p>No hay eventos en esta sede.</p>
        )}
        {admin && (
          <button onClick={() => setEditing(null)}>Crear evento</button>
        )}
        {admin && editing !== undefined && (
          <EventForm
            key={editing?.id ?? "new"}
            event={editing}
            versions={versions.data ?? []}
            onCancel={() => setEditing(undefined)}
            onSave={async (data) => {
              if (editing)
                await apiClient.PUT(
                  "/api/v1/tenants/{tenantId}/venues/{venueId}/events/{eventId}",
                  {
                    params: {
                      path: { tenantId, venueId, eventId: editing.id },
                    },
                    body: data,
                  },
                );
              else
                await apiClient.POST(
                  "/api/v1/tenants/{tenantId}/venues/{venueId}/events",
                  { ...scope(tenantId, venueId), body: data },
                );
              setEditing(undefined);
              await refresh();
            }}
          />
        )}
      </section>
      {tenantAdmin && (
        <details className="card">
          <summary>Archivar sede</summary>
          <p>
            La sede se ocultará sin borrar sus datos. Las sedes con eventos
            publicados se conservan activas.
          </p>
          <button
            className="danger"
            onClick={async () => {
              if (!confirm("¿Archivar esta sede?")) return;
              try {
                await apiClient.DELETE(
                  "/api/v1/tenants/{tenantId}/venues/{venueId}",
                  scope(tenantId, venueId),
                );
                await cache.invalidateQueries({ queryKey: ["me"] });
                navigate("/");
              } catch (e) {
                setError((e as Error).message);
              }
            }}
          >
            Archivar esta sede
          </button>
        </details>
      )}
      {error && <p role="alert">{error}</p>}
    </>
  );
}
function EventForm({
  event,
  versions,
  onSave,
  onCancel,
}: {
  event: Event | null;
  versions: Version[];
  onSave: (e: components["schemas"]["EventSave"]) => Promise<void>;
  onCancel: () => void;
}) {
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const tomorrow = new Date(Date.now() + 86400000);
  const day = localTime(tomorrow.toISOString()).slice(0, 10);
  const next = new Date(tomorrow.getTime() + 86400000);
  const nextDay = localTime(next.toISOString()).slice(0, 10);
  const fields = [
    ["startsAt", "Inicio", `${day}T22:00`],
    ["endsAt", "Fin", `${nextDay}T05:00`],
    ["entryOpensAt", "Ingreso desde", `${day}T21:30`],
    ["entryClosesAt", "Ingreso hasta", `${nextDay}T03:00`],
    ["redemptionOpensAt", "Canje desde", `${day}T22:00`],
    ["redemptionClosesAt", "Canje hasta", `${nextDay}T04:30`],
  ] as const;
  return (
    <form
      className="event-form"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setError("");
        const f = new FormData(e.currentTarget);
        const time = (key: string) =>
          new Date(String(f.get(key)) + "-05:00").toISOString();
        try {
          await onSave({
            name: String(f.get("name")),
            capacity: Number(f.get("capacity")),
            startsAt: time("startsAt"),
            endsAt: time("endsAt"),
            entryOpensAt: time("entryOpensAt"),
            entryClosesAt: time("entryClosesAt"),
            redemptionOpensAt: time("redemptionOpensAt"),
            redemptionClosesAt: time("redemptionClosesAt"),
            layoutVersionId: String(f.get("version")) || null,
            expectedRevision: event?.revision ?? 0,
          });
        } catch (e) {
          setError((e as Error).message);
        } finally {
          setBusy(false);
        }
      }}
    >
      <h3>{event ? "Editar borrador" : "Nuevo evento"}</h3>
      <p>
        Fechas y horas en America/Lima. Para eventos nocturnos, selecciona el
        día siguiente en Fin.
      </p>
      <label>
        Nombre del evento
        <input
          name="name"
          defaultValue={event?.name}
          required
          maxLength={160}
        />
      </label>
      <label>
        Aforo del evento
        <input
          name="capacity"
          type="number"
          min={1}
          max={100000}
          defaultValue={event?.capacity ?? 100}
          required
        />
      </label>
      <div className="grid">
        {fields.map(([key, label, initial]) => (
          <label key={key}>
            {label}
            <input
              type="datetime-local"
              name={key}
              defaultValue={event ? localTime(event[key]) : initial}
              required
            />
          </label>
        ))}
      </div>
      <label>
        Versión de mapa
        <select name="version" defaultValue={event?.layoutVersionId ?? ""}>
          <option value="">Seleccionar después (solo borrador)</option>
          {versions.map((v) => (
            <option value={v.id} key={v.id}>
              {v.name} · v{v.number}
            </option>
          ))}
        </select>
      </label>
      {versions.length === 0 && (
        <p>Publica un mapa para poder publicar el evento.</p>
      )}
      {error && <p role="alert">{error}</p>}
      <div className="actions">
        <button disabled={busy}>Guardar borrador</button>
        <button type="button" className="secondary" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </form>
  );
}
export function LayoutEditor() {
  const { tenantId = "", venueId = "", templateId = "" } = useParams();
  const cache = useQueryClient();
  const [message, setMessage] = useState("");
  const q = useQuery({
    queryKey: ["layout", tenantId, venueId, templateId],
    queryFn: () =>
      apiClient
        .GET(
          "/api/v1/tenants/{tenantId}/venues/{venueId}/layouts/{templateId}",
          { params: { path: { tenantId, venueId, templateId } } },
        )
        .then((r) => r.data!),
    refetchOnWindowFocus: false,
  });
  if (q.isPending) return <p>Cargando mapa…</p>;
  if (q.isError) return <p role="alert">{q.error.message}</p>;
  return (
    <>
      <Link to={`/workspace/${tenantId}/${venueId}`}>← Volver a la sede</Link>
      <LayoutDraft
        key={`${templateId}:${q.data.revision}`}
        layout={q.data}
        tenantId={tenantId}
        venueId={venueId}
        onMessage={setMessage}
        onRefresh={async () => {
          await q.refetch();
          await cache.invalidateQueries({ queryKey: ["versions"] });
        }}
      />
      <p role="status">{message}</p>
    </>
  );
}
function LayoutDraft({
  layout,
  tenantId,
  venueId,
  onRefresh,
  onMessage,
}: {
  layout: Layout;
  tenantId: string;
  venueId: string;
  onRefresh: () => Promise<void>;
  onMessage: (s: string) => void;
}) {
  const [elements, setElements] = useState<MapElement[]>(layout.elements),
    [name, setName] = useState(layout.name),
    [selected, setSelected] = useState<string>(),
    [dirty, setDirty] = useState(false),
    [version, setVersion] = useState(""),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const update = (next: MapElement[]) => {
    setElements(next);
    setDirty(true);
    onMessage("");
  };
  const item = elements.find((x) => x.id === selected);
  const path = { tenantId, venueId, templateId: layout.id };
  const published = layout.versions.find((x) => x.id === version);
  async function save() {
    setBusy(true);
    setError("");
    try {
      await apiClient.PUT(
        "/api/v1/tenants/{tenantId}/venues/{venueId}/layouts/{templateId}",
        {
          params: { path },
          body: { name, expectedRevision: layout.revision, elements },
        },
      );
      await onRefresh();
      onMessage("Mapa guardado. Puedes reabrirlo o publicar una versión.");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <p className="eyebrow">EDITOR DE MAPAS · REVISIÓN {layout.revision}</p>
      <h1>{name}</h1>
      <div className="toolbar">
        <label>
          Nombre de plantilla
          <input
            value={name}
            maxLength={160}
            onChange={(e) => {
              setName(e.target.value);
              setDirty(true);
            }}
          />
        </label>
        <label>
          Vista
          <select value={version} onChange={(e) => setVersion(e.target.value)}>
            <option value="">Borrador editable</option>
            {layout.versions.map((v) => (
              <option key={v.id} value={v.id}>
                Publicada v{v.number} · {v.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="actions">
        <button disabled={busy || !dirty} onClick={save}>
          Guardar mapa
        </button>
        <button
          disabled={busy || dirty || !!published}
          onClick={async () => {
            setBusy(true);
            setError("");
            try {
              const r = await apiClient.POST(
                "/api/v1/tenants/{tenantId}/venues/{venueId}/layouts/{templateId}/publish",
                {
                  params: { path },
                  body: { expectedRevision: layout.revision },
                },
              );
              await onRefresh();
              onMessage(
                `Versión ${r.data!.number} publicada. Los eventos existentes conservan su mapa.`,
              );
            } catch (e) {
              setError((e as Error).message);
            } finally {
              setBusy(false);
            }
          }}
        >
          Publicar mapa
        </button>
        <button
          className="secondary"
          onClick={async () => {
            if (
              dirty &&
              !confirm("¿Descartar cambios sin guardar y reabrir el mapa?")
            )
              return;
            setElements(layout.elements);
            setName(layout.name);
            setDirty(false);
            setSelected(undefined);
            await onRefresh();
            onMessage("Borrador reabierto desde el servidor.");
          }}
        >
          Reabrir borrador
        </button>
      </div>
      <p>
        {published
          ? `Versión publicada ${published.number} · Solo lectura`
          : dirty
            ? "Cambios sin guardar. Guarda antes de publicar."
            : "Borrador guardado. Arrastra elementos o usa el panel para mover, redimensionar y rotar."}
      </p>
      {!published && (
        <div className="palette" aria-label="Paleta de elementos">
          {palette.map(([kind, label]) => (
            <button
              key={kind}
              onClick={() => {
                const decoration = [
                  "Stage",
                  "Bar",
                  "Entrance",
                  "Service",
                ].includes(kind);
                const e: MapElement = {
                  id: crypto.randomUUID(),
                  name: label,
                  kind,
                  mode: decoration
                    ? "Decoration"
                    : kind === "General" || kind === "VIP"
                      ? "Capacity"
                      : "Exclusive",
                  capacity: decoration ? 0 : kind === "General" ? 80 : 10,
                  geometry: {
                    x: 0.08 + (elements.length % 3) * 0.28,
                    y: 0.08 + (Math.floor(elements.length / 3) % 3) * 0.27,
                    width: 0.22,
                    height: 0.18,
                    rotation: 0,
                  },
                };
                update([...elements, e]);
                setSelected(e.id);
              }}
            >
              + {label}
            </button>
          ))}
        </div>
      )}
      <div className="editor-grid">
        <div>
          <MapCanvas
            elements={published?.elements ?? elements}
            selected={published ? undefined : selected}
            onSelect={published ? undefined : setSelected}
            onChange={published ? undefined : update}
          />
          <MapList elements={published?.elements ?? elements} />
        </div>
        {!published &&
          (item ? (
            <ElementProperties
              element={item}
              onChange={(e) =>
                update(elements.map((x) => (x.id === e.id ? e : x)))
              }
              onDelete={() => {
                update(elements.filter((x) => x.id !== item.id));
                setSelected(undefined);
              }}
            />
          ) : (
            <aside className="properties">
              <h3>Propiedades</h3>
              <p>
                Añade o selecciona un elemento del mapa. Las coordenadas también
                se pueden ajustar con teclado.
              </p>
            </aside>
          ))}
      </div>
      {error && <p role="alert">{error}</p>}
    </>
  );
}
export function PublicEvents() {
  const [venue, setVenue] = useState(""),
    [date, setDate] = useState("");
  const [filters, setFilters] = useState({ venue: "", date: "" });
  const q = useQuery({
    queryKey: ["public-events"],
    queryFn: () => apiClient.GET("/api/v1/public/events").then((r) => r.data!),
  });
  const events = q.data?.filter(
    (e) =>
      (!filters.venue || e.venueId === filters.venue) &&
      (!filters.date || limaDay(e.startsAt) === filters.date),
  );
  const venues = [
    ...new Map(q.data?.map((e) => [e.venueId, e.venueName])).entries(),
  ];
  return (
    <div className="n-public">
      <section className="n-hero">
        <NochePhoto file="hero" />
        <div>
          <p className="n-eyebrow">VIVE LA NOCHE · HORA DE LIMA</p>
          <h1>
            Tu próxima noche
            <br />
            empieza <em>aquí.</em>
          </h1>
          <p>Descubre eventos y reserva un espacio para tu grupo.</p>
          <a className="n-btn" href="#events">
            Explorar eventos <span aria-hidden="true">↗</span>
          </a>
        </div>
      </section>
      <form
        className="n-filters"
        onSubmit={(e) => {
          e.preventDefault();
          setFilters({ venue, date });
        }}
        onReset={() => {
          setVenue("");
          setDate("");
          setFilters({ venue: "", date: "" });
        }}
      >
        <div className="n-field">
          <label htmlFor="n-venue">Sede</label>
          <select
            id="n-venue"
            value={venue}
            onChange={(e) => setVenue(e.target.value)}
          >
            <option value="">Todas las sedes</option>
            {venues.map(([id, name]) => (
              <option key={id} value={id}>
                {name}
              </option>
            ))}
          </select>
        </div>
        <div className="n-field">
          <label htmlFor="n-date">Fecha</label>
          <input
            id="n-date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
        </div>
        <button className="n-btn">Buscar eventos</button>
        <button className="n-btn n-btn--secondary" type="reset">
          Limpiar
        </button>
      </form>
      <section id="events">
        <div className="n-section-heading">
          <div>
            <p className="n-eyebrow">LA AGENDA</p>
            <h2>Elige tu próxima noche</h2>
          </div>
          <span className="n-muted" aria-live="polite">
            {events ? events.length + " eventos" : ""}
          </span>
        </div>
        {q.isPending ? (
          <p>Cargando eventos…</p>
        ) : q.isError ? (
          <p role="alert">{q.error.message}</p>
        ) : events?.length ? (
          <div className="n-grid">
            {events.map((e, index) => (
              <article className="n-card" key={e.id}>
                <NochePhoto file={index % 2 ? "dj" : "hero"} loading="lazy" />
                <div className="n-card__body">
                  <p className="n-eyebrow">{e.venueName}</p>
                  <h3>{e.name}</h3>
                  <p className="n-muted">{dateText(e.startsAt)}</p>
                  <div className="n-card__footer">
                    <span className="n-small n-muted">
                      Entradas y experiencias
                    </span>
                    <Link className="n-btn" to={"/events/" + e.id}>
                      Ver evento →
                    </Link>
                  </div>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <p className="n-empty">
            {q.data?.length
              ? "No hay eventos con estos filtros."
              : "Aún no hay eventos publicados."}
          </p>
        )}
      </section>
      <p className="n-small n-muted n-illustrative">
        Fotografías ilustrativas · Consulta los detalles y las ofertas de cada
        evento.
      </p>
    </div>
  );
}
export function PublicEvent() {
  const { eventId = "" } = useParams();
  const catalog = useCatalog(eventId);
  const [view, setView] = useState("map");
  const [zoomed, setZoomed] = useState(false);
  const [selected, setSelected] = useState<string>();
  useEffect(() => {
    setSelected(undefined);
  }, [eventId]);
  const q = useQuery({
    queryKey: ["public-event", eventId],
    queryFn: () =>
      apiClient
        .GET("/api/v1/public/events/{eventId}", {
          params: { path: { eventId } },
        })
        .then((r) => r.data!),
  });
  useEffect(() => {
    if (!selected || !q.data || !catalog.data) return;
    const resource = q.data.resources.find(
      (r) => r.sourceElementId === selected,
    );
    if (
      !resource ||
      !catalog.data.offers.some((o) => o.resourceId === resource.id) ||
      !catalog.data.inventory.some(
        (i) => i.resourceId === resource.id && i.state === "Available",
      )
    )
      setSelected(undefined);
  }, [selected, q.data, catalog.data]);
  if (q.isPending) return <p>Cargando evento…</p>;
  if (q.isError) return <p role="alert">{q.error.message}</p>;
  const e = q.data;
  const availability = Object.fromEntries(
    e.resources.map((r) => {
      const i = catalog.data?.inventory.find((i) => i.resourceId === r.id);
      const names: Record<string, string> = {
        Available: "Disponible",
        Reserved: "Reservado",
        Sold: "Vendido",
        Blocked: "Bloqueado",
      };
      return [
        r.sourceElementId,
        {
          state: i?.state ?? "",
          label: i
            ? names[i.state] + " · " + i.available + " libres"
            : "Consultando…",
        },
      ];
    }),
  );
  const pick = (id: string) => {
    const resource = e.resources.find((r) => r.sourceElementId === id);
    if (
      resource &&
      catalog.data?.offers.some((o) => o.resourceId === resource.id) &&
      availability[id]?.state === "Available"
    )
      setSelected(id);
  };
  const resource = e.resources.find((r) => r.sourceElementId === selected);
  return (
    <div className="n-public">
      <section className="n-hero n-event-hero">
        <NochePhoto file="dj" />
        <div>
          <Link to="/">← Volver a eventos</Link>
          <p className="n-eyebrow">{e.venueName} · HORA DE LIMA</p>
          <h1>{e.name}</h1>
          <p>
            {dateText(e.startsAt)} → {dateText(e.endsAt)}
          </p>
        </div>
      </section>
      <details className="n-schedules">
        <summary>Horarios y aforo · {e.capacity} personas</summary>
        <p>
          Ingreso: {dateText(e.entryOpensAt)} → {dateText(e.entryClosesAt)}
        </p>
        <p>
          Canje: {dateText(e.redemptionOpensAt)} →{" "}
          {dateText(e.redemptionClosesAt)}
        </p>
      </details>
      <div className="n-booking-layout">
        <section className="n-venue">
          <p className="n-eyebrow">TU LUGAR PARA ESTA NOCHE</p>
          <h2>Mapa del local</h2>
          <p className="n-muted">
            Elige un espacio para consultar sus ofertas.
          </p>
          <div className="n-view-switch">
            <button
              className="n-btn n-btn--secondary"
              aria-pressed={view === "map"}
              onClick={() => setView("map")}
            >
              Ver mapa
            </button>
            <button
              className="n-btn n-btn--secondary"
              aria-pressed={view === "list"}
              onClick={() => setView("list")}
            >
              Ver lista
            </button>
            {view === "map" && (
              <button
                className="n-btn n-btn--secondary"
                aria-pressed={zoomed}
                onClick={() => setZoomed(!zoomed)}
              >
                {zoomed ? "Ajustar mapa" : "Ampliar mapa"}
              </button>
            )}
          </div>
          <div className="n-map-wrap">
            {view === "map" ? (
              <div
                className="n-map-scroll"
                tabIndex={zoomed ? 0 : undefined}
                aria-label="Vista desplazable del mapa"
              >
                <div className={zoomed ? "n-map-zoom" : undefined}>
                  <MapCanvas
                    elements={e.elements}
                    selected={selected}
                    onSelect={pick}
                    availability={availability}
                    presentation
                    selectableIds={e.resources
                      .filter(
                        (r) =>
                          catalog.data?.offers.some(
                            (o) => o.resourceId === r.id,
                          ) &&
                          availability[r.sourceElementId]?.state ===
                            "Available",
                      )
                      .map((r) => r.sourceElementId)}
                  />
                </div>
              </div>
            ) : (
              <div className="n-resource-list" aria-label="Lista de elementos">
                {e.elements.map((item) => (
                  <button
                    className="n-btn n-btn--secondary"
                    key={item.id}
                    disabled={
                      item.mode === "Decoration" ||
                      availability[item.id]?.state !== "Available" ||
                      !e.resources.some(
                        (r) =>
                          r.sourceElementId === item.id &&
                          catalog.data?.offers.some(
                            (o) => o.resourceId === r.id,
                          ),
                      )
                    }
                    aria-pressed={selected === item.id}
                    onClick={() => pick(item.id)}
                  >
                    <strong>{item.name}</strong>
                    <span>
                      {item.capacity ? item.capacity + " personas · " : ""}
                      {availability[item.id]?.label ?? "Decorativo · Sin venta"}
                    </span>
                  </button>
                ))}
              </div>
            )}
            <div className="n-legend">
              <span>
                <i className="n-swatch" />
                Disponible
              </span>
              <span>
                <i className="n-swatch n-swatch--selected" />
                Seleccionado
              </span>
              <span>
                <i className="n-swatch n-swatch--reserved" />
                Reservado
              </span>
              <span>
                <i className="n-swatch n-swatch--sold" />
                Vendido
              </span>
            </div>
          </div>
          <p className="n-small n-muted">
            La disponibilidad del espacio puede variar según la oferta. El cupo
            y el precio se confirman al reservar.
          </p>
        </section>
        <EventShop
          key={eventId}
          eventId={eventId}
          resourceId={resource?.id}
          clearSelection={() => setSelected(undefined)}
        />
      </div>
    </div>
  );
}
