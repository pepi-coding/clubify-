# contract.md — Plataforma multidiscoteca

Versión 1.0 · 14/11/2026 · Contrato de implementación para Codex.

## Cambio de alcance autorizado — pagos manuales del MVP

El usuario solicitó el 14/11/2026, después de F4 y antes de avanzar a otras fases: nuevas reservas online con plazo de **una hora**, apertura de **WhatsApp** al terminar la reserva y envío de captura de **Yape/Plin** por el cliente. Un **Promotor** autorizado por empresa/sede verifica el abono y confirma pago/reserva en la aplicación; solo entonces se emiten beneficios. Esta instrucción sustituye el checkout simulado y hold de diez minutos para ese flujo. Se conservan idempotencia, control de aforo, auditoría, emisión atómica y tratamiento de pagos tardíos sin sobreventa. No implica avanzar a F5–F9 ni desplegar un servicio externo. Reglas concretas: `docs/whatsapp-payments.md`.

## 1. Instrucción de ejecución

Desarrolla por fases una plataforma multiempresa para discotecas usando React, C# y PostgreSQL. Este archivo es ejecutable como guía de trabajo, no un contrato legal. Lee también `documento-tecnico-discotecas.md` si está disponible; este contrato es autónomo para comenzar.

**Si el usuario solo entrega este archivo, implementa F0 y detente con un resultado ejecutable y comprobado.** Si solicita una fase específica o varias fases, ejecuta las solicitadas y sus dependencias faltantes. No pidas confirmación para tareas reversibles ya comprendidas en la fase autorizada. No avances automáticamente de una fase a la siguiente si no fue solicitada.

Cada fase debe terminar con funcionalidad verificable, migraciones pertinentes, pruebas críticas, documentación y un resumen de lo que ya funciona. No declares una fase completada por tener pantallas con datos mock o botones sin efecto. Si faltan credenciales externas, implementa y verifica la alternativa local, describe el bloqueo del despliegue y no inventes resultados.

Antes de modificar un repositorio existente, lee sus instrucciones y estado. Conserva trabajo ajeno; adapta la estructura sin regenerar todo. En un repositorio nuevo usa la estructura propuesta. No contratar servicios ni publicar con dinero real como consecuencia de este documento.

## 2. Producto y alcance

Jerarquía: plataforma → empresa/franquicia (`Tenant`) → sede (`Venue`) → evento (`Event`). Cada sede tiene plantillas de mapa; cada evento conserva una versión publicada y su inventario.

Cliente: consulta eventos, selecciona entrada/box/paquete, reserva temporalmente, paga y obtiene pases individuales y vales independientes. Seguridad registra ingresos; barra registra entregas. Administradores gestionan sus empresas/sedes, ofertas y cortesías. Caja registra ventas presenciales utilizando las mismas reglas de inventario.

**MVP demostrable = F0 a F6.** Los pagos son simulados; persistencia, permisos, reservas, emisión y escaneos son reales. **F7 = habilitación comercial. F8 = React Native. F9 = ampliaciones.** No desarrollar F7–F9 dentro del MVP ni anunciarlo como apto para cobrar antes de superar F7.

Supuestos predeterminados:

- Idioma español, moneda PEN, fechas visibles en `America/Lima`, persistencia UTC.
- Pago completo, sin adelantos, reingreso ni operación offline.
- Cliente autenticado; acceso a sus propios pedidos. Compra invitado posterior.
- Box exclusivo: una compra ocupa todo el box y emite el número definido de accesos.
- VIP define zona; gratis define precio/origen. Son atributos independientes.
- Ninguna emisión gratuita omite el control de capacidad.
- Consumo se habilita cuando al menos un pase de su pedido registró ingreso.
- No stock físico de almacén: se controla el saldo de consumos comprados.

## 3. Tecnología obligatoria y estructura

- React + TypeScript + Vite, React Router, TanStack Query y Tailwind CSS.
- C# / ASP.NET Core .NET 10 LTS, EF Core y Npgsql compatibles.
- PostgreSQL local con Docker Compose; Supabase PostgreSQL cloud.
- Identity para cuentas y cookies seguras de sesión web; autorización por políticas.
- REST `/api/v1`, OpenAPI y cliente TypeScript generado a partir del contrato.
- Docker de producción multietapa que sirve React y API desde un mismo origen.
- xUnit para dominio e integración; PostgreSQL real para concurrencia; Playwright para E2E.
- Sin microservicios, CQRS obligatorio, bus externo, Redis ni arquitectura adicional sin necesidad concreta.
- React Native con Expo se crea únicamente en F8, usando la API existente.

Estructura propuesta:

| Ruta | Contenido |
|---|---|
| `src/backend/ClubPlatform.Api` | HTTP, autenticación, políticas, configuración y composición |
| `src/backend/ClubPlatform.Core` | Dominio, casos de uso e interfaces de integración |
| `src/backend/ClubPlatform.Infrastructure` | EF, Identity, pagos, cifrado y jobs |
| `src/web` | Aplicación React responsive pública, cliente y personal |
| `tests/backend` | Pruebas de dominio e integración |
| `tests/e2e` | Flujos Playwright |
| `docs` | Arquitectura, decisiones, operación y progreso |
| `infra` | Dockerfile, Compose y configuración de despliegue |
| `src/mobile` | Solo desde F8 |

Usa versiones estables y compatibles y registra archivos lock. Fija el SDK .NET y la versión mayor PostgreSQL compatibles con el proyecto Supabase elegido. No dependas de `latest` en imágenes de producción. Documenta versiones efectivamente probadas.

## 4. Invariantes obligatorios

1. El backend determina precios, permisos, empresa, fecha y disponibilidad.
2. Todo dato empresarial incorpora `TenantId` y relaciones que evitan referencias entre empresas; cada sede/evento también se valida.
3. Un usuario cliente no obtiene membresía administrativa por comprar.
4. No se sobrevende un box ni el cupo de una zona ni el aforo total del evento.
5. Entrada, reserva de espacio y consumo son beneficios independientes.
6. Un pase registra a lo sumo un ingreso en MVP.
7. Un consumo nunca queda con saldo negativo.
8. Reintentos de compra/pago/escaneo/canje no duplican efectos.
9. Pagos simulados no funcionan en Production; el inicio de la aplicación debe fallar si se configuran allí.
10. No hay éxito falso sin conexión, persistencia o confirmación de operación.
11. Mapas, ofertas y precios comprados quedan versionados o en snapshot.
12. Ventas y auditoría no desaparecen por borrados de catálogo ni cascadas.

## 5. Modelo mínimo y decisiones de negocio

### Identidad y permisos

Roles: PlatformAdmin, TenantAdmin, VenueManager, Cashier, Security, BarStaff y Customer. Modelar membresías por empresa y asignaciones por sede; no un campo de rol global para todo el personal. PlatformAdmin administra empresas pero no recibe lectura implícita de sus ventas.

Todas las rutas administrativas comprueban sesión, membresía activa y sede. A otro tenant se responde 404; a falta de permiso dentro del alcance, 403. Aplicar la misma política a reportes, SQL manual y jobs. Jobs resuelven tenant y lo hacen explícito al operar.

Web: cookies HttpOnly/Secure/SameSite, token CSRF en mutaciones y sesiones revocables. Desarrollo local HTTP es excepción documentada. Persistir claves de Data Protection y cifrado fuera del contenedor efímero; en demo se aceptan secretos persistentes del entorno, nunca generados silenciosamente en cada arranque. No tokens de sesión en localStorage.

### Mapas, ofertas e inventario

Guardar plantilla, versiones y elementos; JSONB para geometría y tablas para recursos e inventario. SVG con coordenadas normalizadas. Modos `Exclusive`, `Capacity`, `Decoration`. Evento publicado usa snapshot; después del primer hold no admite cambios estructurales.

Oferta versionada: precio, moneda, composición e inventarios que consume. Pedido guarda snapshot. Recurso exclusivo con disponibilidad uno, aunque su capacidad humana sea diez. Contabilizar esos diez accesos contra aforo global sin descontar diez entradas generales por accidente. Bebidas sueltas no crean pases nuevos.

Inventario relacional: fila por evento/recurso con cantidades held/sold y límite, más contador de aforo global. Ledger de asignaciones por hold/pedido. Dentro de una transacción bloquear filas relevantes en orden determinista, liberar vencidos pertinentes y validar todos los contadores; crear/confirmar/liberar juntos. Checks y restricciones únicas refuerzan las invariantes. No usar locks en memoria.

### Reservas, pedidos y pagos

Hold de diez minutos por defecto, configurable. Reloj del servidor; UI solo muestra cuenta regresiva. Cada consulta y mutación crítica reconoce expiraciones, aunque el worker esté retrasado.

- Hold: Active / Converted / Expired / Cancelled.
- Pedido: PendingPayment / Confirmed / Expired / Cancelled / PaymentReview.
- Intento de pago: Created / Pending / Succeeded / Failed / RefundPending / Refunded.

Usar `IPaymentGateway` y adaptador Demo. Confirmación bloquea pedido/hold/inventario y emite beneficios en una sola transacción. Un pedido admite varios intentos pero una sola emisión de beneficios. Si un pago tardío llega después de perder inventario, registrar el pago y `PaymentReview`, sin pases ni sobreventa. La demo debe permitir reproducir este caso como incidencia, sin simular que el cliente ya tiene acceso.

Dinero en céntimos con enteros de 64 bits y límites seguros para JSON/JS; no float. API recibe identificadores y cantidades, no totales confiables. Todas las líneas de un pedido pertenecen a una misma empresa y evento en MVP.

### Pases y consumos

Ticket individual con ID interno, token aleatorio opaco de 256 bits, hash de búsqueda, token cifrado para mostrarlo al dueño, evento, sede, zona, estado y ordinal de emisión. Restricción única por pedido/línea/ordinal. No usar JWT como prueba autosuficiente de entrada ni PII en QR.

Vale de consumo separado con líneas concedidas y cantidades entregadas. Canje exige personal autorizado, ventana válida y al menos un ingreso del pedido; bloqueo de líneas y registro atómico. Escanear/inspeccionar no entrega ni consume; confirmar sí.

Token de QR y sesión son credenciales distintas. POST para enviarlos; no registrarlos en logs. Rate limits; mensajes operativos claros. Operaciones inválidas no exponen detalles de compradores ajenos.

### Idempotencia y auditoría

`Idempotency-Key` en hold, pedido, pago demo, ingreso y canje. Persistir alcance por actor/tenant/operación, hash del cuerpo y resultado; misma clave/cuerpo devuelve resultado original, distinta carga da 409. También usar restricciones únicas y transacciones: la clave sola no es garantía de exclusión. Definir y documentar retención de registros.

Auditar creación de cortesías, bloqueos, cambios de oferta, pagos, ingresos y canjes con actor, tenant, sede, fecha y correlación. Auditoría sin tokens ni datos de tarjeta. Estados finales no se alteran por un simple CRUD.

## 6. F0 — Base ejecutable

**Objetivo:** preparar un proyecto reproducible, sin construir todavía ventas.

Entregar solución backend y React, PostgreSQL Compose, `.env.example` sin secretos, migración inicial de infraestructura, health endpoints, manejo de errores y página que consulte API y muestre conectividad. Crear README con arranque local y `docs/progress.md` marcando F0 terminada/F1 pendiente. Configuración por entorno y prohibición del proveedor Demo en Production desde el inicio.

Verificar instalación limpia, build backend/frontend, migraciones en DB vacía y health de API/DB. Diferenciar `/health/live` de readiness de DB; no filtrar secretos en respuestas. Documentar comandos reales, no comandos hipotéticos. No crear pantallas de catálogo con funcionalidades ficticias.

**Aceptación:** una persona siguiendo README inicia React, API y DB; obtiene respuesta persistente de health/readiness y builds exitosos. Si el entorno no tiene SDK/Docker, informar verificaciones bloqueadas sin dar F0 por validada.

## 7. F1 — Identidad y aislamiento empresarial

**Depende de F0.** Implementar Identity, login/logout, CSRF, membresías y asignaciones, selección de contexto y navegación por rol. Alta de empresas/usuarios de demo mediante seed idempotente, solo habilitado explícitamente en Development/Demo. No contraseñas de demo universales incluidas en producción; recibirlas por entorno.

Seed: empresa A con dos sedes; empresa B con una sede; administradores separados; seguridad y barra asignadas; cliente sin membresías de personal. Login real, sin selector de rol que sustituya autenticación.

**Aceptación:** admin A no lee ni modifica sede B cambiando IDs; empleado de una sede no opera otra; cliente no abre administración; logout revoca sesión; mutación sin CSRF es rechazada. Probar mediante API además de ocultar botones. Página de administración vacía con datos persistidos, sin necesidad de catálogo aún.

## 8. F2 — Sedes, mapas y eventos

**Depende de F1.** CRUD de sedes y borradores de evento; editor SVG con paleta de elementos, mover/redimensionar/rotar y panel de propiedades. Agregar, guardar, reabrir y publicar versiones. Capacidad, modo de venta y nombres. Mostrar mapa publicado y alternativa de lista responsive.

Crear evento con inicio/fin, ventana de ingreso y canje y aforo total. Publicar solo con configuración válida. El aforo del evento no puede superar el límite configurado de la sede. Copiar recursos desde versión publicada.

**Aceptación:** mover y guardar elementos sobrevive al reload; sede A y B tienen mapas independientes; evento conserva versión aunque se edite plantilla; recurso decorativo no se vende. Evento nocturno termina correctamente al día siguiente. Editor no necesita CAD ni colaboración en tiempo real.

## 9. F3 — Ofertas, disponibilidad y holds

**Depende de F2.** Productos de bebida, ofertas de entradas/boxes/paquetes, versiones y reglas de consumo de inventario. Catálogo público por empresa/evento; mapa con disponible/reservado/vendido/bloqueado. Precios calculados por backend.

Implementar holds, vencimiento y cancelación; worker de limpieza persistente. Al comprar se vuelven a verificar expiraciones dentro de la transacción. Claves idempotentes y contadores relacionales desde esta fase.

Seed del evento A: aforo 100; dos boxes de diez personas; zona general de 80; box B3 + diez pases + whisky x1 + gaseosa x2 por S/ 1,000. La oferta del box usa recurso exclusivo uno y aforo diez. Cliente ve cupo y precio; todavía no hay emisión de pases hasta F4.

**Aceptación:** dos compradores concurrentes por B3 producen exactamente un hold exitoso; el otro recibe 409. No reservar por encima de cupo/aforo. Al vencer, se recupera capacidad. Repetir petición con la misma clave no descuenta dos veces; cambiar cuerpo con la misma clave da 409. Ejecutar pruebas en PostgreSQL real, incluyendo concurrencia sobre general y aforo global.

## 10. F4 — Venta demo y emisión

**Depende de F3.** Crear pedido desde hold, checkout simulado etiquetado, historial del cliente, detalle y QR individuales; vale de consumo independiente. Venta presencial de caja y cortesías con motivo mediante los mismos casos de uso de inventario. En demo caja registra cliente existente o uno de demostración; no inventa cobros reales.

Ofrecer resultados demo aprobado/rechazado, protegidos por entorno y permisos; no un endpoint anónimo que confirme cualquier pedido. Snapshot de precio y composición. Emisión atómica y reconsulta de pases para el propietario, con claves de cifrado persistentes.

**Aceptación:** compra del box emite diez tickets distintos y un vale con tres unidades en dos líneas. Pago rechazado no emite beneficios. Repetición/concurrencia de confirmaciones produce una sola emisión. Pago tardío sin inventario queda en PaymentReview. Cortesía consume aforo, puede ser general o VIP y queda auditada. Reiniciar API conserva capacidad de mostrar QR del pedido. No afirmar que compartir un QR equivale a transferencia de titularidad.

## 11. F5 — Seguridad y barra

**Depende de F4.** Interfaces responsive separadas por permisos, cámara HTTPS, selección de evento y alternativa de pegar/ingresar código. Preconsulta y confirmación separadas. Feedback visible, textual y sonoro; manejar permiso de cámara denegado, poca conectividad y reintentos.

Seguridad: ingreso atómico; respuestas de válido, usado, revocado, incorrecto o fuera de horario. Sin reingreso ni contadores de ocupación actual. Barra: detalle de saldo, selección de líneas/cantidades y confirmación de entrega parcial; no marca todo automáticamente al leer QR.

**Aceptación:** dos escaneos simultáneos admiten una sola vez. Ticket de otra sede/evento no entra; vale no sirve como entrada; seguridad no canjea y barra no emite cortesías. Dos empleados intentando entregar la última botella producen un solo canje. Entregar una gaseosa deja botella x1 y gaseosa x1. Reintentar una operación por fallo de red recupera el resultado sin doble entrega. Fallo de conexión nunca muestra acceso/canje aprobado.

## 12. F6 — Demo completa y despliegue

**Depende de F5. Fin del MVP.** Panel por evento con pedidos confirmados, importe simulado, entradas emitidas, ingresados, cortesías y consumos pendientes. Separar ventas de cortesías y dinero simulado de real. Filtros por empresa/sede/evento; sin reporte contable ficticio.

Pulir estados de carga/error/vacío, móvil, navegación y accesibilidad básica. Preparar Docker que compila React y C#, sirve mismo origen y respeta puerto del host. Rutas SPA funcionan al recargar; `/api/inexistente` responde 404 JSON y no HTML.

Preparar configuración Render y guía Supabase DB-only, secretos, conexiones IPv4/IPv6, usuario restringido, esquema no expuesto, persistencia de claves y migraciones como tarea única. Health y logs estructurados. No guardar archivos de negocio en disco efímero. Desplegar demo solo si hay credenciales y autorización de despliegue existentes; de lo contrario entregar configuración lista y señalar el paso pendiente.

**Aceptación:** E2E desde publicación de evento y compra hasta ingreso/canje/reporte; tenant B sin acceso a los datos. Pruebas críticas F1–F5 pasan. README incluye guion de demo, cuentas por rol mediante configuración, reset local seguro y limitaciones. Reset demo nunca corre contra Production. Lista explícita de componentes simulados y pendientes de F7.

Guion de demostración:

1. Admin A publica evento y muestra mapa.
2. Cliente compra box B3 por S/ 1,000 usando pago demo.
3. Muestra diez QR de acceso y vale con whisky x1/gaseosa x2.
4. Seguridad admite a dos invitados; repetición de un QR es rechazada.
5. Barra entrega una gaseosa y deja los demás saldos.
6. Admin ve resultados; empresa B no puede consultarlos.

## 13. F7 — Piloto comercial con dinero real

**No forma parte del MVP de demostración.** Requiere definición de proveedor, credenciales sandbox/producción y empresa que recibe los fondos. Primera operación: una empresa receptora; no implementar marketplace ni reparto automático de fondos sin requerimiento específico.

Entregar integración real detrás de `IPaymentGateway`; validación de webhook según proveedor, importe/moneda/referencia, inbox y outbox persistentes, reintentos y reconciliación de pagos. Pago externo no se ejecuta dentro de una transacción larga de DB. No confirmar con redirect del navegador. Probar eventos duplicados/desordenados y pago tardío. Definir qué ocurre si dos intentos cobran el mismo pedido: una emisión y segundo cargo a revisión/devolución, nunca ignorar el dinero.

Agregar devoluciones/anulaciones con permisos y movimientos compensatorios. Revocar beneficios no utilizados cuando corresponde; consumos/ingresos usados requieren resolución explícita. Evento cancelado dispara flujo controlado y no borra ventas. Recuperación de cuenta, verificación de contacto y MFA administrativa. Correo de entrega con outbox; enlaces seguros y expiración si se incorpora compra invitado.

Operación: paid hosting/DB, prueba de restauración y política RPO/RTO aceptada, alertas, runbook de incidentes, reconciliación y pruebas con equipos/red del local. Medir carga de referencia 50 compradores, 10 escáneres y 5 barras concurrentes; preservar invariantes y acordar objetivos de latencia con el cliente. Separar ambiente y credenciales de demo/producción; no seed de prueba en prod.

**Aceptación:** sandbox de pago validado, conciliación y devolución verificadas, simulación deshabilitada en Production, recuperación probada y acta de pendientes comerciales resuelta. Credenciales o decisiones faltantes bloquean solo la habilitación correspondiente; no se sustituye por un éxito simulado. Publicar para dinero real exige instrucción explícita del usuario, no se deriva de completar código.

## 14. F8 — App React Native

**Después del MVP; piloto móvil real depende de F7.** Expo + React Native + TypeScript, selección de versiones compatibles en ese momento. Prioridad inicial cliente: eventos, compras y pases. Portales administrativos conservan web. Añadir escáner nativo de personal solo si se solicita.

Reutilizar API/OpenAPI, cliente TypeScript y validaciones de presentación compartibles; no asumir reutilización automática de componentes DOM. Implementar autenticación móvil separada y segura: flujo de autorización compatible con cliente público, tokens cortos y refresh rotativo/revocable, almacenamiento seguro del sistema, sin secretos incrustados. Elegir solución mantenida en la fase; no crear un servidor OAuth improvisado.

**Aceptación:** Android/iOS de prueba consultan el mismo evento y pedido que la web; pago retorna por deep link verificable; pase emitido se valida con el backend existente; sesión y revocación funcionan. Publicación en tiendas y cuentas de desarrollador requieren alcance y acceso propios. No agregar entrada offline sin diseño independiente.

## 15. F9 — Expansión opcional

Priorizar por evidencia de uso: promotores y comisiones, preventas por lotes, cupones avanzados, adelantos, inventario de almacén/barra, pedidos desde el box, facturación, fidelización y cobro SaaS por empresa. Cada módulo necesita alcance y criterios propios. Operación offline, pagos multiempresa y reingreso son cambios de negocio y arquitectura, no simples toggles.

## 16. Entrega y validación de cada fase

Actualizar `docs/progress.md` con estado, cambios, migraciones, instrucciones, pruebas ejecutadas, resultados, limitaciones y siguiente fase. Estados permitidos: pendiente, en curso, implementada con verificación pendiente, completada. No llamar completada a una fase con una verificación crítica bloqueada.

En el resumen al usuario indicar qué funciona, cómo ejecutarlo y qué sigue. No hacer preguntas por nombres temporales, colores o preferencias menores: usar `ClubPlatform`, interfaz clara en español, fondo oscuro en cliente y controles de operación de alto contraste. Las decisiones que cambian cobro, seguridad o alcance se registran y resuelven antes de la fase afectada.

Ejecutar builds y pruebas pertinentes a la fase; no inflar cobertura con pruebas que solo repiten setters. Dar prioridad a invariantes, aislamiento y fallos concurrentes. No usar SQLite/EF InMemory para demostrar comportamiento PostgreSQL. Pruebas locales deben ser reproducibles y no modificar bases remotas.

## 17. Prompts de inicio y continuación

Inicio: «Lee contract.md y documento-tecnico-discotecas.md. Implementa únicamente F0. Comprueba que se puede ejecutar localmente, documenta resultados y deja el progreso preparado para F1».

Continuación: «Revisa contract.md, el repositorio y docs/progress.md. Implementa F1 y sus dependencias faltantes. Respeta el trabajo existente, valida criterios de aceptación y actualiza el progreso. No avances a F2».

Autorización de varias fases: «Implementa F2 y F3 según contract.md, verificando y documentando cada una. Detente al terminar F3».
