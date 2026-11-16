import React, { useState } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Routes, Route, Link, Navigate } from "react-router-dom";
import {
  QueryClient,
  QueryClientProvider,
  useQuery,
} from "@tanstack/react-query";
import "./style.css";
import { Checkout, MyOrders, OrderDetail, CommerceWorkspace } from "./Commerce";
import { PaymentVerificationWorkspace } from "./ManualPayments";
import { OperationsWorkspace } from "./Operations";
import {
  LayoutEditor,
  PlanningWorkspace,
  PublicEvents,
  PublicEvent,
  VenueCreation,
} from "./Planning";
import type { components } from "./api/schema";
type Venue = components["schemas"]["VenueResponse"];
type Me = components["schemas"]["MeResponse"];
const client = new QueryClient({
  defaultOptions: { queries: { retry: false } },
});
import { apiClient } from "./api/client";
function App() {
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => apiClient.GET("/api/v1/auth/me").then((r) => r.data!),
  });
  const health = useQuery({
    queryKey: ["health"],
    queryFn: async () => {
      const r = await fetch("/health/ready");
      if (!r.ok) throw Error();
      return r.json();
    },
  });
  const [error, setError] = useState("");
  async function logout() {
    try {
      await apiClient.POST("/api/v1/auth/logout");
      client.clear();
      location.assign("/");
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <>
      <header>
        <Link to="/" className="brand">
          CLUB<span>PLATFORM</span>
        </Link>
        <nav>
          {me.data ? (
            <>
              <span>{me.data.email}</span>
              <Link to="/orders">Mis pedidos</Link>
              <button onClick={logout}>Cerrar sesión</button>
            </>
          ) : (
            <Link to="/login">Iniciar sesión</Link>
          )}
        </nav>
      </header>
      <main>
        {error && <p role="alert">{error}</p>}
        <Routes>
          <Route
            path="/"
            element={
              <>
                <p className="eyebrow">SEDES, MAPAS Y EVENTOS</p>
                <h1>
                  Tu operación,
                  <br />
                  en su espacio.
                </h1>
                <p>Acceso seguro por empresa, sede y función.</p>
                <p className="status">
                  {health.isPending
                    ? "Comprobando conexión…"
                    : health.isError
                      ? "Base de datos no disponible"
                      : "API y base de datos conectadas"}
                </p>
                <PublicEvents />
                {me.isPending ? (
                  <p>Cargando sesión…</p>
                ) : me.data ? (
                  <>
                    <h2>Hola, {me.data.email}</h2>
                    <VenueCreation memberships={me.data.memberships} />
                    {me.data.memberships.length ? (
                      <>
                        <p>
                          Selecciona una sede para abrir tu espacio de trabajo.
                        </p>
                        <div className="grid">
                          {me.data.venues.map((v) => (
                            <Link
                              className="card"
                              key={v.id}
                              to={`/workspace/${v.tenantId}/${v.id}`}
                            >
                              <small>
                                {
                                  me.data!.memberships.find(
                                    (m) => m.tenantId === v.tenantId,
                                  )?.name
                                }
                              </small>
                              <h3>{v.name}</h3>
                              <span>
                                {
                                  me.data!.memberships.find(
                                    (m) => m.tenantId === v.tenantId,
                                  )?.role
                                }{" "}
                                →
                              </span>
                            </Link>
                          ))}
                        </div>
                      </>
                    ) : (
                      <section className="card">
                        <h2>Mi cuenta</h2>
                        <p>
                          Tu cuenta de cliente está activa. Consulta los eventos
                          publicados y reserva entradas o paquetes.
                        </p>
                      </section>
                    )}
                    {me.data.isPlatformAdmin && <Platform />}
                  </>
                ) : (
                  <Link className="primary" to="/login">
                    Acceder a mi cuenta →
                  </Link>
                )}
              </>
            }
          />
          <Route
            path="/workspace/:tenantId/:venueId/layouts/:templateId"
            element={<LayoutEditor />}
          />
          <Route path="/events/:eventId" element={<PublicEvent />} />
          <Route path="/checkout/:holdId" element={<Checkout />} />
          <Route path="/orders" element={<MyOrders />} />
          <Route path="/orders/:orderId" element={<OrderDetail />} />
          <Route
            path="/login"
            element={me.data ? <Navigate to="/" /> : <Login />}
          />
          <Route
            path="/workspace/:tenantId/:venueId"
            element={<Workspace me={me.data} />}
          />
          <Route
            path="*"
            element={
              <p>
                Página no encontrada. <Link to="/">Volver al inicio</Link>
              </p>
            }
          />
        </Routes>
      </main>
      <footer>ClubPlatform · Reservas y pagos por WhatsApp</footer>
    </>
  );
}
function Login() {
  const [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  return (
    <section className="card login">
      <p className="eyebrow">BIENVENIDO</p>
      <h1>Iniciar sesión</h1>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          const f = new FormData(e.currentTarget);
          try {
            await apiClient.POST("/api/v1/auth/login", {
              body: {
                email: String(f.get("email")),
                password: String(f.get("password")),
              },
            });
            await client.invalidateQueries({ queryKey: ["me"] });
          } catch (e) {
            setError((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <label>
          Correo electrónico
          <input
            name="email"
            type="email"
            required
            autoComplete="username"
            maxLength={254}
          />
        </label>
        <label>
          Contraseña
          <input
            name="password"
            type="password"
            required
            autoComplete="current-password"
            maxLength={256}
          />
        </label>
        {error && <p role="alert">{error}</p>}
        <button className="primary" disabled={busy}>
          {busy ? "Verificando…" : "Entrar"}
        </button>
      </form>
    </section>
  );
}
import { useParams } from "react-router-dom";
function Workspace({ me }: { me: Me | undefined }) {
  const { tenantId, venueId } = useParams();
  const [message, setMessage] = useState("");
  const venue = useQuery({
    queryKey: ["venue", tenantId, venueId],
    queryFn: () =>
      apiClient
        .GET("/api/v1/tenants/{tenantId}/venues/{venueId}", {
          params: { path: { tenantId: tenantId!, venueId: venueId! } },
        })
        .then((r) => r.data!),
  });
  const role = me?.memberships.find((m) => m.tenantId === tenantId)?.role;
  if (venue.isPending) return <p>Cargando sede…</p>;
  if (venue.isError) return <p role="alert">{venue.error.message}</p>;
  const base = `/tenants/${tenantId}/venues/${venueId}`;
  return (
    <>
      <Link to="/">← Mis sedes</Link>
      <p className="eyebrow">ESPACIO DE TRABAJO · {role}</p>
      <h1>{venue.data.name}</h1>
      <p>Zona horaria: {venue.data.timeZone}</p>
      <button
        onClick={async () => {
          try {
            await apiClient.PUT(
              "/api/v1/tenants/{tenantId}/venues/{venueId}/context",
              { params: { path: { tenantId: tenantId!, venueId: venueId! } } },
            );
            setMessage("Contexto guardado en tu sesión.");
            client.invalidateQueries({ queryKey: ["me"] });
          } catch (e) {
            setMessage((e as Error).message);
          }
        }}
      >
        Usar esta sede
      </button>
      {["TenantAdmin", "VenueManager"].includes(role ?? "") && (
        <section className="card">
          <h2>Administración de sede</h2>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              const name = new FormData(e.currentTarget).get("name");
              try {
                await apiClient.PUT(
                  "/api/v1/tenants/{tenantId}/venues/{venueId}",
                  {
                    params: {
                      path: { tenantId: tenantId!, venueId: venueId! },
                    },
                    body: {
                      name: String(name),
                      capacity: Number(
                        new FormData(e.currentTarget).get("capacity"),
                      ),
                    },
                  },
                );
                await client.invalidateQueries({ queryKey: ["venue"] });
                setMessage("Nombre guardado.");
              } catch (e) {
                setMessage((e as Error).message);
              }
            }}
          >
            <label>
              Nombre
              <input
                name="name"
                defaultValue={venue.data.name}
                maxLength={160}
                required
              />
            </label>
            <label>
              Aforo de la sede
              <input
                name="capacity"
                type="number"
                min={1}
                max={100000}
                defaultValue={venue.data.capacity}
                required
              />
            </label>
            <button>Guardar nombre</button>
          </form>
        </section>
      )}
      <section className="card">
        <h2>
          {role === "Security"
            ? "Seguridad"
            : role === "BarStaff"
              ? "Barra"
              : role === "Cashier"
                ? "Caja"
                : "Operación"}
        </h2>
        <p>Selecciona un evento de tu sede y opera según tus permisos.</p>
      </section>
      {(role === "Security" || role === "BarStaff") && (
        <OperationsWorkspace
          tenantId={tenantId!}
          venueId={venueId!}
          mode={role === "Security" ? "admission" : "redemption"}
          actor={me?.email ?? ""}
        />
      )}
      {["TenantAdmin", "VenueManager", "Cashier"].includes(role ?? "") && (
        <CommerceWorkspace
          tenantId={tenantId!}
          venueId={venueId!}
          admin={role !== "Cashier"}
        />
      )}
      {["TenantAdmin", "VenueManager", "Promoter"].includes(role ?? "") && (
        <PaymentVerificationWorkspace
          tenantId={tenantId!}
          venueId={venueId!}
          admin={role !== "Promoter"}
        />
      )}
      {role !== "Security" && role !== "BarStaff" && (
        <PlanningWorkspace
          tenantId={tenantId!}
          venueId={venueId!}
          admin={["TenantAdmin", "VenueManager"].includes(role ?? "")}
          tenantAdmin={role === "TenantAdmin"}
        />
      )}
      <p role="status">{message}</p>
    </>
  );
}
function Platform() {
  const q = useQuery({
    queryKey: ["tenants"],
    queryFn: () =>
      apiClient.GET("/api/v1/platform/tenants").then((r) => r.data!),
  });
  return (
    <section className="card">
      <h2>Empresas de la plataforma</h2>
      {q.isError ? (
        <p role="alert">{q.error.message}</p>
      ) : (
        q.data?.map((t) => <p key={t.id}>{t.name}</p>)
      )}
    </section>
  );
}
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={client}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>,
);
