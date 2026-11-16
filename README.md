# ClubPlatform

Implementación de F0–F4 de `contract.md`. React/TypeScript + ASP.NET Core Identity + PostgreSQL real. Interfaz en español, zona horaria America/Lima. Incluye sedes, mapas, eventos, ofertas versionadas, reservas, venta demo, pases QR y vales de consumo.

**Flujo actual del MVP:** reserva de **60 minutos** → apertura de **WhatsApp** → pago por **Yape/Plin** y captura enviada al negocio → verificación por **Promotor** → reserva confirmada y QR emitidos. Antes de recibir reservas, AdminA configura el número en Miraflores → **Reservas y pagos por WhatsApp → Configurar WhatsApp de pagos**. No se ha inventado un número de contacto. Reglas y pagos vencidos: [docs/whatsapp-payments.md](docs/whatsapp-payments.md).

## Demo completa en Docker

```powershell
./scripts/docker-demo.ps1
```

Abrir [la demo local](http://localhost:5081). El contenedor `clubplatform-f4-preview` sirve frontend y API; `infra-db-1` sirve PostgreSQL. El script compila, aplica migraciones como tarea separada y conserva la DB y las claves persistentes. Sustituye la preview F2 en ese puerto y deja su contenedor detenido. `-SkipBuild` reutiliza la imagen y `-Port 5082` cambia el puerto.

Iniciar como Customer, abrir **Noche demo · Boxes y entradas** y reservar B3 por S/ 1,000. WhatsApp abre con referencia, importe y límite; enviar allí la captura. Como `promoter@demo.local`, abrir Miraflores → **Reservas y pagos por WhatsApp**, seleccionar evento y verificar abono. **Mis pedidos** muestra diez QR individuales y un vale de whisky ×1/gaseosa ×2 después de confirmar. Contraseña del promotor: `Seed__Passwords__Promoter` en `.local/settings.json`. AdminA también puede verificar y gestionar ofertas/cortesías.

El flujo online usa verificación humana de Yape/Plin; no hay pasarela ni comprobación automática del abono. El modo Demo previo se conserva para pruebas y no puede autoaprobar reservas WhatsApp. Ingreso y canje corresponden a F5. Reglas originales F3/F4: [docs/f3-f4.md](docs/f3-f4.md); cambios actuales: [docs/whatsapp-payments.md](docs/whatsapp-payments.md).

## Requisitos

- .NET SDK **10.0.404** (`global.json`); Node **22.15.0** o una versión 22 compatible; Docker Desktop con motor Linux activo; PowerShell.
- En esta máquina se instaló el SDK de forma local en `%LOCALAPPDATA%/boboteca-dotnet`. `scripts/dev-env.ps1` lo detecta. En otra máquina instalar el SDK indicado en `global.json`.
- Puertos locales 55432 (PostgreSQL), 5080 (API) y 5173 (Vite).

## Arranque limpio

Desde la raíz del repositorio:

```powershell
./scripts/setup.ps1
```

El script crea explícitamente `.local/settings.json` con contraseñas aleatorias por cuenta y una contraseña de PostgreSQL. No se regeneran en los siguientes arranques. El archivo está ignorado; protegerlo como secreto local. El seed recibe las contraseñas mediante variables de entorno, no tiene contraseñas universales. Si ya existe el volumen PostgreSQL con otra contraseña, conservar la configuración original: cambiar POSTGRES_PASSWORD no cambia una base ya inicializada.

Terminal API:

```powershell
. ./scripts/dev-env.ps1 -Seed
dotnet run --project src/backend/ClubPlatform.Api
```

Terminal web:

```powershell
cd src/web
npm run dev
```

Abrir [la aplicación local](http://localhost:5173). Vite reenvía `/api` y `/health` al backend; no se necesita CORS. El seed solo se ejecuta con `Seed__Enabled=true` y en Development/Demo. Es idempotente y no cambia contraseñas ni nombres existentes. Después del primer arranque se puede omitir `-Seed`.

## Cuentas demo

Consultar las contraseñas privadas en `.local/settings.json` (`Seed__Passwords__…`).

| Clave | Correo | Alcance |
|---|---|---|
| AdminA | admin.a@demo.local | Empresa A, Miraflores y Barranco |
| AdminB | admin.b@demo.local | Empresa B, Centro |
| Security | security@demo.local | Seguridad, A/Miraflores |
| Bar | bar@demo.local | Barra, A/Miraflores |
| Customer | customer@demo.local | Cliente sin membresías de personal |
| Cashier | cashier@demo.local | Caja demo, A/Miraflores |
| Promoter | promoter@demo.local | Verificación manual, A/Miraflores |

No existe selector que sustituya el login. Los administradores gestionan sedes, mapas, eventos, ofertas y cortesías. El promotor verifica abonos de Yape/Plin en su sede. Caja demo permanece disponible solo con el proveedor Demo de pruebas. Seguridad/barra consultan eventos de su sede asignada; sus escáneres se implementan en F5. Clientes autenticados reservan por WhatsApp y consultan sus propios pedidos.

## Comprobaciones

```powershell
. ./scripts/dev-env.ps1
Invoke-RestMethod http://localhost:5080/health/live
Invoke-RestMethod http://localhost:5080/health/ready
dotnet build --no-restore
dotnet test tests/backend
cd src/web
npm ci
npm run generate:api
npm run build
```

`generate:api` requiere la API en 5080 y regenera `src/api/schema.d.ts` desde `/openapi/v1.json`. `openapi-fetch` usa ese contrato tipado para las solicitudes reales. Los tests xUnit crean bases temporales `clubtest_<guid>` en PostgreSQL **local** y las eliminan al finalizar; no usan SQLite ni EF InMemory. La cuenta local necesita CREATEDB. `TestDatabase__Connection` se configura con dev-env; sin ella las pruebas usan la conexión de desarrollo inicial. Nunca apuntar pruebas a una base remota.

E2E, con API y Vite iniciados y seed aplicado:

```powershell
. ./scripts/dev-env.ps1
cd tests/e2e
npm ci
npx playwright install chromium
npm test
```

Los doce escenarios combinan escritorio y móvil: identidad/permisos, flujo completo de mapa/evento, reserva WhatsApp, rechazo/corrección y verificación del promotor, diez QR y vale, publicación de oferta y cortesía. Las 31 pruebas backend incluyen configuración/plazo de una hora, permisos del promotor, comprobantes reutilizados, concurrencia, aforo, idempotencia, expiración, pagos repetidos/tardíos, snapshots, cifrado y regresiones F0–F4.

## Migraciones y Docker

```powershell
. ./scripts/dev-env.ps1
dotnet tool restore
dotnet run --project src/backend/ClubPlatform.Api -- --migrate
docker build -f infra/Dockerfile -t clubplatform:f4 .
```

Las migraciones son un comando explícito, no se ejecutan automáticamente al servir solicitudes. El Dockerfile multietapa entrega React y API desde el mismo origen, como usuario no root en puerto 8080. Para ejecutarlo configurar `ConnectionStrings__Database` y `DataProtection__Path` y montar ahí almacenamiento persistente accesible por el usuario 1654. Production exige HTTPS en el proxy; cookies Secure siempre fuera de Development. El proveedor de pagos predeterminado es Disabled; Demo y el seed de prueba provocan fallo de inicio en Production. No se ha desplegado ningún servicio externo ni configurado Supabase.

## Seguridad y operación

- Cookies HttpOnly/SameSite=Strict; Secure fuera de Development. HTTP local es la excepción deliberada. Nunca se guardan tokens en localStorage.
- Obtener `/api/v1/auth/csrf` y enviar `X-CSRF-TOKEN` en todas las mutaciones, incluido login. Renovar el token después de iniciar/cerrar sesión. Sin token válido: 400.
- Sesiones de ocho horas en PostgreSQL, comprobadas en cada solicitud; logout revoca la sesión en servidor. Cambiar el security stamp invalida cookies existentes. Los cambios de membresía/asignación se aplican en cada operación.
- Autorización por políticas y filtros explícitos: empresa ajena 404, permiso insuficiente dentro de la empresa 403, sesión inválida 401. PlatformAdmin es un permiso de plataforma; no da acceso implícito a sedes ni ventas. Su aprovisionamiento no se incluye en el seed demo.
- El contexto seleccionado se persiste en sesión; cada ruta vuelve a comprobar sus IDs y permisos.
- Login limita intentos por IP (20/minuto) y aplica bloqueo Identity tras cinco fallos por cuenta. No hay recuperación de cuenta/MFA hasta F7.
- Claves Data Protection en `.local/keys`, persistentes entre reinicios. Localmente se guardan sin cifrado de disco gestionado por la aplicación: proteger permisos/backup; producción requiere volumen/secretos protegidos. La API rechaza configuración sin ruta de claves.
- Liveness no consulta DB; readiness exige conexión y migraciones aplicadas y no revela credenciales. Errores API usan Problem Details; rutas `/api/*` inexistentes no devuelven HTML.
- F1 conserva sesiones para revocación; no hay limpieza automática todavía. Plan de retención: eliminar solo sesiones revocadas/vencidas de más de 30 días mediante mantenimiento autorizado. F3/F4 conservan registros idempotentes y auditoría durante toda la demo, sin purga automática.

Resultados, decisiones y límites: `docs/progress.md` y `docs/architecture.md`.

## F2: sedes, mapas y eventos

1. Entrar con AdminA y seleccionar una sede (o expandir **Crear sede**).
2. En **Administración de sede**, configurar su aforo. Las sedes existentes de F1 reciben un valor inicial de **100** durante la migración: revisarlo antes de publicar eventos.
3. En **Mapas de la sede**, crear una plantilla y abrir el editor.
4. Añadir boxes, mesas, zonas, escenario, barra, accesos o servicios. Arrastrar elementos para moverlos; usar el panel para cambiar nombre, capacidad, modo, posición, ancho, alto y rotación. Guardar antes de publicar.
5. Publicar una versión. El selector **Vista** permite consultar las versiones anteriores como solo lectura; el borrador sigue siendo editable.
6. Volver a la sede y crear un evento con aforo, inicio/fin y ventanas de ingreso/canje. Las fechas son de Lima; un evento nocturno debe terminar en la fecha siguiente. Elegir la versión publicada y guardar el borrador.
7. Publicar el evento y abrir **Ver mapa publicado**. La agenda pública también lo muestra. **Ver lista** ofrece una alternativa responsive al SVG.
8. Editar y volver a publicar la plantilla: el evento anterior conserva su versión y los recursos copiados.

Solo borradores de evento admiten edición/eliminación. Eliminar una sede la archiva y conserva sus datos; se rechaza si ya tiene eventos publicados. Los recursos decorativos nunca se copian al inventario de recursos del evento. F3/F4 añaden ofertas, inventario held/sold, holds con vencimiento y ventas demo sobre esos recursos.

Detalles de reglas y API: `docs/f2.md` y `docs/f3-f4.md`. Las E2E crean sedes, eventos y pedidos locales de nombre `E2E …`; los datos publicados y las ventas se conservan como demostración.
