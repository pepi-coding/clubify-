# Plataforma multidiscoteca — Documento técnico

Versión 1.0 · 14 de noviembre de 2026 · Estado: propuesta de arquitectura para desarrollo.
Nombre comercial pendiente. Este documento define el producto y sus decisiones técnicas; `contract.md` define cómo implementarlo por fases. No es un contrato legal ni una cotización.

## 1. Objetivo y límites

Construir una plataforma multiempresa para vender entradas y paquetes de espacios y bebidas, configurar la distribución de cada sede y controlar ingresos y entregas mediante QR.

La primera entrega será un MVP web responsive que se pueda demostrar al cliente de principio a fin. Persistirá datos reales en PostgreSQL, aplicará permisos y control de concurrencia, pero utilizará un proveedor de pagos simulado claramente identificado. No procesará tarjetas ni dinero real.

Después se habilitará un piloto comercial con pagos reales y medidas operativas verificadas. La app móvil se desarrollará posteriormente con React Native y reutilizará la API y las reglas del backend.

### Alcance del MVP demostrable

- Dos empresas aisladas, varias sedes y eventos por fecha.
- Acceso de administrador de empresa y sede, caja, seguridad y barra.
- Editor visual sencillo de mapas; catálogo público de eventos y disponibilidad.
- Entradas generales/VIP, boxes exclusivos y paquetes con bebidas.
- Compra, pago simulado, venta presencial y emisión de pases/vales.
- Pases individuales para invitados de un box y consumo independiente.
- Cortesías autorizadas, escaneo por cámara e ingreso manual del código.
- Entregas parciales, auditoría e indicadores básicos.

Fuera del MVP: pagos reales, facturación electrónica, inventario de almacén, comisiones de promotores, adelantos, reingreso, funcionamiento offline, multimoneda, marketplace con liquidaciones, app nativa y suscripciones automáticas de empresas. Estas capacidades tienen fases posteriores; no deben aparecer como funciones operativas falsas.

## 2. Arquitectura recomendada

| Capa | Decisión |
|---|---|
| Web | React + TypeScript + Vite; TanStack Query, React Router y Tailwind CSS |
| Servidor | C# con ASP.NET Core sobre .NET 10 LTS, actualizado al parche soportado |
| Persistencia | PostgreSQL; EF Core y proveedor Npgsql de versión compatible |
| Identidad | ASP.NET Core Identity; cookies seguras en la web; autenticación móvil en fase posterior |
| API | REST `/api/v1`, OpenAPI y DTO explícitos; Problem Details para errores |
| Distribución visual | SVG y coordenadas normalizadas; editor sencillo sin motor 3D |
| Pruebas | xUnit e integración sobre PostgreSQL real; Playwright para flujos críticos |
| Despliegue inicial | Un contenedor Docker en Render que sirve API y build de React |
| Base de datos cloud | Supabase PostgreSQL; acceso exclusivo desde el backend |
| Móvil posterior | React Native con Expo; mismo backend y contratos HTTP |

Monolito modular: un despliegue y una base de datos. Módulos de identidad, organizaciones, sedes/mapas, eventos, catálogo, reservas, pedidos/pagos, pases, canjes y reportes. No introducir microservicios, Redis ni un bus externo para el MVP.

La web y la API comparten origen: `https://dominio/` y `https://dominio/api/v1/`. El servidor entrega React para rutas de navegación, nunca como fallback de una ruta `/api` inexistente. Esto simplifica cookies y despliegue. Se puede separar el frontend en un CDN más adelante sin cambiar el dominio de negocio.

### Flujo de confianza

React y React Native llaman por HTTPS a ASP.NET Core. Solo ASP.NET Core accede a PostgreSQL. El proveedor de pago comunica resultados al backend; la página de retorno del comprador no confirma el pago. Ningún cliente decide precios, pertenencia empresarial, permisos ni validez de QR.

## 3. Cloud: decisión y presupuesto orientativo

**Recomendación: C# en Render + PostgreSQL en Supabase, usando Supabase únicamente como base de datos.** Supabase no sustituye el hosting de ASP.NET Core. No se requiere Supabase Auth, Realtime, Edge Functions ni su Data API para este diseño.

| Entorno | Configuración propuesta | Referencia mensual USD |
|---|---|---|
| Desarrollo | Docker local con PostgreSQL y API; Vite local | Sin gasto cloud obligatorio |
| Demo compartida | Render con 512 MB de pago + Supabase Free | Desde 7, dentro de cuotas |
| Piloto de producción | Render 1 CPU / 2 GB + Supabase Pro con un proyecto inicial | Base aproximada de 50 |
| Producción con workspace Render Pro | Configuración anterior más workspace Pro | Base aproximada de 75 |

Importes de referencia consultados el 14/11/2026, no cotización ni promesa de capacidad. Excluyen impuestos, dominio, correo, comisiones de pagos, tráfico adicional, almacenamiento, staging y complementos. El plan de workspace y el cómputo de Render se cobran separadamente. Revisar condiciones comerciales y configuración antes de contratar. Los 512 MB son una opción de demo a medir; para piloto se propone 2 GB.

El plan gratuito de Supabase puede pausarse por inactividad y tiene cuotas; reservarlo para demo. Los servicios gratuitos de Render se suspenden por inactividad, por lo que no se recomiendan para puerta/barra ni para una presentación sin preparación. Producción tendrá cómputo de pago y base de datos de pago.

### Conectividad y seguridad

- Elegir regiones cercanas entre API y DB. Punto de partida: Virginia en Render y región de costa este equivalente en Supabase, sujeto a disponibilidad. Medir latencia desde Perú y entre ambos servicios antes de fijar región; no combinar regiones distantes por preferencia nominal.
- Usar conexión PostgreSQL directa si la red soporta IPv6; si solo soporta IPv4, usar pooler de sesión. No adoptar transaction pooling sin verificar sus restricciones y el comportamiento del ORM.
- TLS con validación de certificado; Npgsql con límites explícitos de pool y timeouts. El límite total suma todas las instancias y tareas.
- Esquema propio `app` no expuesto por Data API, sin permisos para roles públicos `anon`/`authenticated`. Configurar Data API deshabilitada cuando esté disponible y verificar ausencia de acceso público.
- Credencial de aplicación sin superusuario ni DDL; credencial separada para migraciones. No usar `postgres` como usuario de ejecución.
- No instalar SDK de Supabase en React para consultar tablas del negocio. No incluir connection strings ni claves administrativas en variables `VITE_*`.
- No depender del disco efímero de Render. QR se generan bajo demanda; en demo las imágenes serán assets del proyecto. Para archivos subidos posteriormente, agregar almacenamiento de objetos a través de una interfaz de backend.

### Operación y recuperación

Despliegues reproducibles con Docker multietapa. CI ejecuta build, pruebas y validaciones; CD ejecuta migraciones como tarea única antes de publicar, sin migraciones simultáneas al iniciar cada réplica. Usar cambios compatibles con la versión anterior; revertir la imagen no revierte automáticamente la base de datos.

Supabase Pro incluye backups diarios con retención de siete días según la documentación consultada; PITR es un complemento separado. Objetivo inicial propuesto: RPO de 24 horas y RTO de 4 horas, sujetos a prueba de restauración. Antes de vender entradas, el cliente debe aceptar la pérdida potencial de transacciones recientes o financiar una política de recuperación más exigente. No asumir que backup diario equivale a recuperación de los últimos minutos.

Mantener copia cifrada externa y comprobar restauración en una DB aislada. No confundir backup de DB con backup de objetos. Alertas de salud, errores, conexiones y fallos de webhook; sin QR completos ni datos de pago sensibles en logs.

Una instancia de API y un proyecto DB son un punto inicial, no alta disponibilidad garantizada. Ajustar réplicas, recursos y recuperación tras pruebas de carga y requisitos comerciales.

## 4. Organización y permisos

`Tenant` representa la empresa/franquicia comercial. `Venue` representa una sede. `Event` representa una fecha concreta con horario y mapa publicado. Un usuario puede pertenecer a varias empresas mediante membresías; sus permisos de personal pueden limitarse a sedes.

| Rol | Alcance |
|---|---|
| PlatformAdmin | Alta/suspensión de empresas; sin acceso implícito a ventas de todas las empresas |
| TenantAdmin | Sedes, personal, eventos, ofertas, reportes y cortesías de su empresa |
| VenueManager | Administración de sus sedes y eventos asignados |
| Cashier | Ventas y consulta de pedidos de su sede; sin modificar pagos confirmados |
| Security | Verificación y registro de ingreso en sede/evento autorizados |
| BarStaff | Consulta mínima y canje de consumos en sede/evento autorizados |
| Customer | Sus pedidos y pases; sin permisos administrativos |

La selección de empresa en UI no es autorización. El servidor valida membresía y alcance en cada solicitud, incluyendo descargas y reportes. Las rutas públicas solo exponen datos publicados. Los clientes globales no necesitan membresía de personal.

Aislamiento: todas las entidades de negocio llevan `TenantId`; consultas filtradas por contexto autenticado y restricciones relacionales compuestas que impiden referencias entre empresas. Filtros globales de EF ayudan, pero no reemplazan autorización de comandos, SQL y jobs. El acceso público deriva la empresa del evento publicado; no confía en un `TenantId` arbitrario del comprador. Se prueban ataques entre empresas en lectura y escritura.

## 5. Mapas y eventos

Mapa de plantilla por sede y versiones inmutables publicadas por evento. Guardar geometría en JSONB y recursos vendibles en tablas relacionales: la geometría no es la autoridad del stock.

Elementos: box, mesa, zona general, zona VIP, escenario, barra, acceso y servicio. Cada uno tiene identificador estable, nombre, coordenadas normalizadas, tamaño, rotación y tipo. Los elementos vendibles incluyen capacidad y modo `Exclusive` o `Capacity`.

- Exclusive: un pedido reserva todo el box/mesa; su capacidad limita los pases emitidos.
- Capacity: varias compras comparten el cupo, como general/VIP.
- Decoration: referencia visual sin precio ni inventario.

Editor MVP: agregar, mover, redimensionar, rotar, eliminar y editar propiedades; guardar borrador, previsualizar y publicar. Sin edición colaborativa ni planos CAD. Diseñar paneles táctiles y alternativa mediante lista en móvil.

El evento copia una versión del mapa y crea su inventario. Después del primer hold o venta, no se modifica su estructura; para el MVP se permiten cambios de texto no estructurales y bloquear disponibilidad libre. Reubicaciones y rediseño con ventas quedan para una fase posterior.

## 6. Catálogo y beneficios

Una oferta vendible tiene precio en céntimos (`bigint` en DB, `long` en C#; límites de API seguros para JS), moneda `PEN`, vigencia y versión. Descuentos se calculan en servidor. El pedido conserva snapshots de nombre, precio y composición; modificar el catálogo no modifica compras anteriores.

Separar lo que ocupa inventario de lo que se entrega. Ejemplo S/ 1,000:

| Componente | Resultado |
|---|---|
| Box B3 | Una asignación exclusiva para el evento |
| Acceso de 10 personas | Diez pases individuales, ligados al box y zona |
| Whisky x1 | Derecho de consumo por una botella |
| Gaseosa x2 | Derecho de consumo por dos unidades canjeables parcialmente |

Los diez accesos del box cuentan contra el aforo total del evento. No descuentan diez entradas generales además del box, salvo que una regla explícita del recurso lo establezca. Toda oferta define explícitamente sus consumos de inventario; no inferirlos del nombre comercial.

Para el MVP, una oferta se asocia a un recurso exclusivo o a un cupo de zona; puede incluir bebidas. Las bebidas adicionales no consumen cupo de ingreso. No hay selección libre de bebidas equivalentes ni consumo mínimo convertible en saldo.

Gratis y VIP son ejes independientes: cortesía define importe/origen; VIP define acceso. Una cortesía exige emisor autorizado, motivo y auditoría, y consume aforo.

## 7. Compra, concurrencia y pagos

### Reserva temporal

1. El cliente selecciona evento/oferta y cantidades; el servidor calcula precio e inventario.
2. Crear hold con vencimiento inicial de diez minutos, configurable y medido con reloj del servidor.
3. Bloquear en transacción PostgreSQL los recursos y contadores afectados en orden determinista. Liberar holds vencidos pertinentes y comprobar cupo efectivo.
4. Registrar hold y asignaciones atómicamente. Un segundo comprador recibe `409 RESOURCE_UNAVAILABLE` cuando corresponde.
5. Reutilizar clave idempotente para reintentos. No usar locks en memoria ni disponibilidad calculada únicamente en el navegador.

Usar filas de inventario por evento/recurso con contadores reservados y vendidos, checks de no negatividad y límite de capacidad; recurso exclusivo con capacidad de asignación uno. Registrar cantidades de acceso separadas y contador de aforo global. Reserva/confirmación/liberación actualizan contadores y ledger en la misma transacción. Todas las rutas, incluidas caja y cortesías, usan esta misma política.

### Estados

| Entidad | Estados principales |
|---|---|
| Evento | Draft → Published → Closed; Cancelled mediante flujo controlado |
| Hold | Active → Converted / Expired / Cancelled |
| Pedido | PendingPayment → Confirmed / Expired / Cancelled; PaymentReview para incidencia |
| Intento de pago | Created → Pending → Succeeded / Failed; RefundPending → Refunded |
| Pase | Issued → Used / Revoked; la validez temporal se evalúa al escanear |
| Consumo | Granted → PartiallyRedeemed → Redeemed / Revoked |

Un pedido puede tener varios intentos de pago. Registrar un pago no implica que el pedido ya pudo asignarse. Si llega pago después del vencimiento y no existe inventario, registrar el dinero y poner el pedido en `PaymentReview`; no revivir reservas ni emitir pases automáticamente. La fase comercial resuelve con devolución o reasignación autorizada y auditable.

Confirmación: verificar proveedor, importe, moneda y referencia. Bloquear pedido, hold e inventario; convertir reserva, confirmar pedido y crear beneficios una sola vez dentro de la misma transacción. Restricciones únicas impiden duplicación ante reintentos.

Pago simulado: disponible solo en Demo/Development, con etiqueta visible, acceso limitado y adaptador `IPaymentGateway`. Producción debe negarse a iniciar con ese adaptador. En fase comercial implementar proveedor real, autenticación del webhook, consulta servidor a servidor cuando corresponda, inbox idempotente y reconciliación. No almacenar tarjetas ni aceptar una captura de pantalla como pago automático. El proveedor y la cuenta receptora se deciden antes de integrar; para el primer piloto se propone una sola empresa operadora, no dispersión de fondos entre muchas empresas.

## 8. QR y control operativo

### Pases

Un pase por persona. El contenido QR es una credencial opaca aleatoria de al menos 256 bits; no contiene DNI, precio ni un identificador incremental. Guardar hash para búsqueda/validación y una copia cifrada para volver a mostrar el pase al dueño. La clave de cifrado es externa a la DB, persistente y respaldada. No registrar el token en logs ni URL de consulta. Credenciales de acceso y consumo son distintas.

La pantalla de seguridad está autenticada. El escáner envía token mediante POST, con sede/evento seleccionados y clave idempotente. El servidor comprueba alcance, estado, ventana de ingreso, evento y sede; marca `Used` y registra ingreso en una transacción. Restricción única de ingreso por pase; escaneos simultáneos solo admiten una persona.

Respuesta clara: admitido; ya utilizado con hora; revocado; sede/evento incorrectos; fuera de horario; inválido. Sin red no mostrar verde ni asumir aprobación. Una preconsulta de detalles no consume el pase. No hay reingreso en MVP y no se presenta el número de ingresados como ocupación actual.

### Consumos

Un vale de consumo por pedido o paquete, con líneas y cantidades restantes. La lectura del QR muestra el detalle; el empleado elige qué y cuánto entregar y confirma. Descontar cantidades y registrar operador, sede, hora y estación de manera atómica; nunca permitir saldo negativo. Consumir dos gaseosas no consume la botella.

El vale no permite ingresar. Para MVP, el canje exige al menos un pase del pedido ya ingresado; bebidas compradas dentro del evento se vinculan a una sesión/pedido con ingreso validado. La entrega se permite en la ventana operativa configurada. No hay anulaciones silenciosas: correcciones por supervisor son movimientos compensatorios auditados, en fase comercial.

Compartir un pase implica compartir la credencial. En MVP se permite mostrar/copiar cada pase individual desde el pedido y se informa que solo admite un uso; no se construye todavía transferencia de titularidad. Recuperación de compras se hace por cuenta autenticada; compra invitado por enlace seguro se pospone al piloto.

## 9. Modelo de datos mínimo

| Grupo | Entidades y datos esenciales |
|---|---|
| Identidad | IdentityUsers, TenantMemberships, VenueAssignments, WebSessions |
| Organización | Tenants, Venues; zona horaria IANA por sede |
| Distribución | LayoutTemplates, LayoutVersions, LayoutElements; geometría JSONB y recursos relacionales |
| Evento | Events, EventResources, EventCapacity; snapshot y ventanas de ingreso/canje |
| Catálogo | Products, Offers, OfferVersions, OfferComponents, InventoryRequirements |
| Reserva | Holds, HoldLines, InventoryAllocations; estado, cantidades y ExpiresAt |
| Venta | Orders, OrderLines, PaymentAttempts; snapshots, comprador, totales y origen |
| Emisión | Tickets, ConsumptionVouchers, ConsumptionEntitlements; hashes y tokens cifrados |
| Operación | AdmissionLogs, RedemptionOperations, RedemptionLines, ScanAttempts |
| Resiliencia | IdempotencyRecords, PaymentInbox, OutboxMessages |
| Auditoría | AuditEntries; actor, empresa, sede, acción, motivo, correlación y fecha |

Fechas `timestamptz` en UTC; visualización según `America/Lima` inicialmente. Eventos que terminan después de medianoche conservan su ID y horario completo. Cantidades enteras positivas, precios no negativos, totales calculados en servidor. No usar float para dinero.

Índices por `(TenantId, EventId)`, estado/vencimiento, hash de credenciales, claves idempotentes, referencia externa de pago y relaciones. Unicidad de beneficios por pedido/línea/ordinal y de ingreso por pase. Claves foráneas compuestas garantizan mismo tenant y evento para asignaciones. Ventas y auditoría no se eliminan por cascada; archivar catálogo y organización.

`PaymentInbox` y outbox se implementan al entrar pagos reales; el MVP ya garantiza emisión transaccional. El worker de expiración puede vivir como HostedService con bloqueo de filas entre réplicas; cada compra también comprueba vencimientos sin depender de la puntualidad del worker.

## 10. Contrato HTTP orientativo

| Grupo | Operaciones |
|---|---|
| Identidad | POST `/auth/login`, `/auth/logout`; GET `/auth/me`, `/auth/csrf` |
| Contexto | GET `/me/memberships`; GET `/admin/venues` con contexto autorizado |
| Mapas | POST `/admin/venues/{id}/layouts`; PUT borrador; POST publicación de versión |
| Eventos | CRUD administrativo y POST `/admin/events/{id}/publish` |
| Público | GET `/public/tenants/{slug}/events`; GET evento, mapa y disponibilidad |
| Compra | POST `/holds`; POST `/orders` desde hold; GET `/me/orders/{id}` |
| Pago demo | POST `/demo/orders/{id}/payment-result`, solo entorno demo y propietario/operador autorizado |
| Cortesía | POST `/admin/events/{id}/complimentary-orders` con motivo |
| Seguridad | POST `/admissions/inspect`; POST `/admissions/check-in` |
| Barra | POST `/redemptions/inspect`; POST `/redemptions` con líneas y cantidades |
| Reportes | GET `/admin/events/{id}/summary`, `/admin/orders` |
| Comercial posterior | POST `/payments/webhooks/{provider}`; rutas de conciliación y devoluciones |

OpenAPI es la fuente de los DTO y del cliente TypeScript generado. Usar `Idempotency-Key` en creación de hold/pedido, confirmación demo, check-in y canje. Alcance por actor/empresa/operación; guardar fingerprint del cuerpo y respuesta; misma clave con distinto cuerpo devuelve 409. Si hay procesamiento pendiente devolver estado recuperable sin repetir efectos.

Respuestas 401 sin sesión, 403 permiso insuficiente, 404 recurso ausente o de otra empresa, 409 conflicto de estado/inventario, 422 validación de negocio. Error incluye `code`, `traceId` y mensaje sin detalles de secretos. Consultas paginadas con límites. No confiar en rutas ocultas para proteger operaciones.

## 11. Seguridad, experiencia y calidad

- Web: cookies HttpOnly/Secure y SameSite, protección CSRF en mutaciones y política de origen. En desarrollo HTTP local es excepción explícita; en cloud HTTPS obligatorio.
- Identity para contraseñas y bloqueo de intentos; sesiones revocables. Recuperación de cuenta y MFA administrativa antes del piloto comercial.
- Rate limits en login, disponibilidad, holds, compras y escaneos, configurados para no bloquear lectores legítimos.
- Tokens y datos personales mínimos; secretos fuera de repositorio. Persistir claves de Data Protection y cifrado para que reinicios no invaliden todo ni pierdan credenciales.
- Cámara requiere HTTPS y permiso del usuario. Alternativa de lector externo/código manual para poca luz; feedback visual, textual y sonoro. No depender solo del color.
- Estados vacíos, carga, errores y reintentos; texto de precios en soles, importe total antes de confirmar y etiquetas de pago simulado.
- Pruebas críticas: aislamiento empresarial, concurrencia de reservas/aforo, pago repetido, pago tardío, ingreso duplicado y canje concurrente/parcial.
- Pruebas de integración sobre PostgreSQL, no EF InMemory/SQLite para simular locks.
- Meta inicial de piloto: p95 de validación API menor a 500 ms desde el entorno de prueba cercano al backend y respuesta de extremo a extremo menor a 2 s en la red real del local. Son objetivos a medir, no garantías del hosting.
- Plan de carga inicial: al menos 50 compradores concurrentes, 10 escáneres y 5 estaciones de barra, ajustar con datos del cliente; stock y saldos deben conservar invariantes incluso bajo saturación.

## 12. Fases y decisiones pendientes

F0 fundamento ejecutable; F1 identidad y empresas; F2 sedes/mapas/eventos; F3 ofertas/reservas; F4 venta demo/emisión; F5 ingreso/canje; F6 demostración y despliegue; F7 piloto comercial; F8 app React Native; F9 expansión. Los criterios detallados están en `contract.md`. El MVP termina en F6.

Antes de F7 confirmar: empresa que cobra, proveedor de pagos, cancelaciones/devoluciones, horario de ingreso/canje, política de cortesías, aforo del local, conectividad, tratamiento de datos y alcance de comprobantes. Si se requiere evaluación normativa específica, realizarla para la operación concreta; este documento no certifica cumplimiento legal.

Supuestos MVP: pago completo; PEN; entrada individual de un solo uso; reserva exclusiva de box; cortesías con cupo; sin offline; cuentas de cliente; sin adelantos ni comisiones. Son decisiones implementables, no requisitos confirmados por el cliente.

## 13. Fuentes oficiales consultadas

Consultadas el 14/11/2026. Precios, versiones y cuotas deben revisarse al contratar.

- .NET y soporte: https://dotnet.microsoft.com/en-us/platform/support/policy
- React Native y recomendación de framework: https://reactnative.dev/docs/environment-setup
- Render Docker: https://render.com/docs/docker
- Render precios: https://render.com/pricing
- Render limitaciones gratuitas: https://render.com/docs/free
- Render regiones: https://render.com/docs/regions
- Supabase precios: https://supabase.com/pricing
- Supabase costo usando solo DB: https://supabase.com/kb/faqs/platform/database-only-cost
- Supabase conexiones PostgreSQL: https://supabase.com/docs/guides/database/connecting-to-postgres
- Supabase backups: https://supabase.com/docs/guides/platform/backups
- Supabase regiones: https://supabase.com/docs/guides/platform/regions
