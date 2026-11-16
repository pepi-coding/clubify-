# Progreso de implementación

Actualizado: 16/11/2026 (America/Lima).

| Fase | Estado |
|---|---|
| F0 — Base ejecutable | completada |
| F1 — Identidad y aislamiento empresarial | completada |
| F2 — Sedes, mapas y eventos | completada |
| F3 — Ofertas, disponibilidad y holds | completada |
| F4 — Venta demo y emisión | completada |
| F5 — Seguridad y barra | completada |
| F6–F9 | pendiente |

## Mejora operativa — referencias cortas

Reservas y pedidos usan una referencia visible de ocho caracteres agrupados XXXX-XXXX, sin 0/1/I/O. PostgreSQL genera códigos únicos entre conexiones/réplicas mediante secuencia no cíclica y permutación/base32, respaldado por índice único y check. Se persisten una sola vez; GUID internos, permisos, inventario y QR permanecen independientes.

Migración `20261116165758_ShortReservationReferences` aplicada sobre la DB existente y bases de prueba. Asigna referencias a pedidos anteriores y actualiza representaciones idempotentes de WhatsApp/pedidos/pagos, sin duplicar efectos ni alterar IDs o beneficios. Mis pedidos, detalle, WhatsApp, cola del promotor y mensajes de venta/cortesía muestran el código. La búsqueda admite minúsculas y ausencia de guion; los GUID antiguos siguen permitidos en búsquedas para mensajes ya enviados.

Verificado: suite completa 40/40 y cuatro pruebas dirigidas, incluidas dos nuevas de unicidad concurrente (512 códigos), rechazo de duplicados DB y migración desde esquema anterior conservando QR/estado/replay. Build backend/frontend y Docker aprobados. Playwright 16/16 escritorio/móvil, incluyendo búsqueda del promotor sin guion y mensaje WhatsApp con código corto sin GUID. Reinicio preserva la referencia y el estado operativo. Documentación: [reservation-references.md](reservation-references.md).
## F5 — Ingreso y canje operativos

Seguridad y barra disponen de paneles separados por rol y sede, selección de evento, cámara QR y alternativa de código pegado/escrito. Leer solo inspecciona; confirmar vuelve a validar permisos, estado y ventanas. Seguridad registra un único ingreso y muestra usados con fecha/hora. Barra exige un ingreso del pedido y entrega únicamente las líneas/cantidades elegidas. Whisky ×1/gaseosa ×2, tras entregar una gaseosa: whisky ×1/gaseosa ×1. No hay reingreso ni ocupación actual.

Transacciones PostgreSQL reutilizan el bloqueo de evento/inventarios y bloquean ticket/vale y líneas ordenadas. Registro de ingreso/entrega, saldo, auditoría e idempotencia se confirman juntos. Unicidad por ticket y FKs compuestas impiden duplicación o vincular otra orden/vale. Reintentar misma clave/cuerpo devuelve el resultado original; clave con otros datos da 409. Se valida la sede del evento antes de recuperar una respuesta idempotente.

La UI distingue consulta pendiente de confirmación, éxito confirmado y rechazo/error; feedback escrito, visual y sonoro. Cámara solicita solo video y se detiene después de leer/salir. Timeout 10 s y diario de operación (clave/hash/estación, sin QR) permiten recuperar una respuesta perdida, incluso tras recargar. Recuperación autenticada y limitada al propio operador. Desconexión nunca aprueba una acción nueva.

Migración `20261116112009_AdmissionsAndRedemptions` aplicada sobre la DB existente y bases PostgreSQL vacías de pruebas. Nuevas tablas AdmissionLog, RedemptionOperation/Line y ScanAttempt; estados y restricciones, sin cascadas comerciales. Pedidos, claves, configuración WhatsApp y QR previos preservados.

Verificado:

- Build backend y frontend aprobados, contrato TS regenerado. Decoder QR se carga solo al abrir cámara; sin advertencia de bundle grande. ZXing fijado a versiones compatibles con Node 22, npm audit sin vulnerabilidades conocidas.
- 38/38 pruebas backend sobre PostgreSQL real: siete F5 nuevas, regresión F0–F4. Incluyen dos empleados por pase/última botella, saldo parcial, reintentos, permisos/CSRF, recuperación por actor, sede/evento/tipo incorrectos, cambio de ventana/revocación antes de confirmar, selección inválida y FK entre vales.
- 16/16 Playwright contra Docker, escritorio/móvil: decoder real con un QR emitido y stream de video sintético, cámara denegada y entrada manual, inspección offline sin aprobación, respuesta perdida después de commit, reintento de ingreso y recuperación de canje tras recargar, sin doble efecto. Se conservan escenarios de WhatsApp, promotor, cortesías, identidad y mapas. El helper de login respeta y espera el límite real de 20/minuto; no se debilitó el servidor.
- Capturas f5-security-{chromium,mobile}.png y f5-bar-{chromium,mobile}.png revisadas sin desbordamiento a 390px. Se ignoran en Git por contener datos operativos de prueba.
- Reinicio real de clubplatform-f5-preview: sesiones de cliente/seguridad/barra, timestamp de admisión, pase Used, saldo parcial y credenciales QR recuperados sin cambios. Script reproducible tests/e2e/restart-operations.mjs; archivos de comparación privados en .local.

Ejecución actual: `./scripts/docker-demo.ps1`, imagen clubplatform:f5 y http://localhost:5081. Se conservan las previews anteriores detenidas y los volúmenes existentes. Usar security@demo.local y bar@demo.local, contraseñas privadas en .local/settings.json. El evento debe tener ventanas vigentes: el seed original de enero de 2027 no permite ingresar antes de esa fecha.

Reglas, API y guion: [f5.md](f5.md). La cámara necesita HTTPS o localhost; no se declara una validación física de todos los teléfonos/redes del local. No hay modo offline ni anulaciones comerciales. Reportes, despliegue y cierre integral de demo corresponden a F6, no iniciada. El video Remotion anterior es un artefacto histórico de F4, donde F5 aparecía como vista previa.

## Ajuste MVP — WhatsApp, Yape/Plin y Promotor

Implementado el cambio solicitado por el usuario antes de avanzar de fase. Nueva reserva online atómica con plazo de 60 minutos y pedido WhatsAppManual; abre WhatsApp con referencia, importe y vencimiento. Captura enviada por el cliente a WhatsApp, sin almacenamiento ni análisis automático en la app. Promotor de sede verifica el abono, método, importe, código de operación, fecha/hora y nota; rechazar permite corregir dentro del plazo original. Aprobación emite una sola vez; verificación vencida registra PaymentReview sin beneficios. No se habilita autoaprobación del cliente en modo manual.

WhatsApp configurable por sede, solo por administrador/manager. Nueva membresía Promoter y cuenta opcional promoter@demo.local; sin permisos de catálogo, cortesías ni otra sede. El 15/11/2026 se configuró Miraflores con el número facilitado por el usuario y se verificó el enlace generado con referencia de reserva, sin enviar mensajes. Las demás sedes permanecen vacías por defecto; nuevas reservas por WhatsApp se deshabilitan hasta configurar el contacto. El número real no se incorpora al código ni al video de prueba.

Migración `20261114191028_WhatsAppManualPayments` aplicada sobre la DB existente y bases vacías de pruebas: configuración por sede, rol y datos de verificación, índice único de operación por empresa/método. Se preservan pedidos anteriores, inventario y claves.

Verificado: build backend sin errores/advertencias; TypeScript/Vite y Docker aprobados; suite PostgreSQL completa 31/31 (cuatro casos manuales nuevos: configuración/plazo/idempotencia, permisos/confirmación concurrente, rechazo y código reutilizado, pago vencido). Playwright 12/12 contra contenedor, escritorio/móvil, incluido flujo nuevo de WhatsApp → rechazo/corrección → promotor → diez QR/vale, más regresión de identidad/mapas/cortesías. Navegación a WhatsApp interceptada con número temporal de prueba y configuración restaurada; no hubo envío de mensajes. Capturas whatsapp-order y whatsapp-promoter revisadas, sin desbordamiento a 390px. Reinicio real del contenedor recupera sesión, mismos diez tickets y vale para ambas compras manuales de navegador.

Preview actual: http://localhost:5081. Script Docker usa WhatsAppManual. Reglas, instrucciones y tratamiento del plazo: [whatsapp-payments.md](whatsapp-payments.md). F5–F9 no iniciadas. Esta sección sustituye el checkout simulado y plazo de diez minutos de la entrega histórica descrita abajo.

## Historial: entrega F3–F4

Ofertas con versiones inmutables y composición de bebidas; catálogo y mapa con disponibilidad/reservado/vendido/bloqueado; precios calculados por servidor. Inventario por recurso y aforo global con checks, ledger y bloqueo transaccional de evento e inventario en PostgreSQL. Holds de diez minutos, cancelación, vencimiento en solicitudes críticas y worker persistente cada 15 segundos. Claves idempotentes por actor/tenant/operación y resultado persistido.

Pedido desde hold; checkout simulado aprobado/rechazado, historial y detalle del dueño. Una confirmación emite beneficios atómicamente; confirmaciones concurrentes no duplican. Pago vencido se registra como PaymentReview sin beneficios. Box B3 por S/ 1,000 emite diez tickets diferentes y un vale de whisky ×1/gaseosa ×2. QR opacos de 256 bits, hash y copia cifrada con claves Data Protection persistentes. Caja demo y cortesías con motivo, permisos de sede y auditoría utilizan el mismo inventario. Cuenta Cashier opcional añadida al seed local.

Migración `20261114183003_CommerceReservationsAndIssuance`: productos, ofertas/versiones, inventarios, holds/líneas/asignaciones, pedidos/snapshots, pagos, tickets, vales/saldos, idempotencia y auditoría. Relaciones compuestas y Restrict conservan aislamiento e historial. Aplicada sobre la DB F2 existente sin borrar datos y en bases vacías de pruebas. El seed comercial es idempotente y no repone ventas al reiniciar.

Verificación:

- Build backend aprobado, cero errores/advertencias. Suite completa: 27/27 pruebas PostgreSQL reales aprobadas, incluidas ocho F3/F4 nuevas. Concurrencia de B3, general y aforo global; idempotencia/conflictos; vencimiento aun sin worker puntual y liberación automática por worker; rechazo, confirmación concurrente, revisión tardía, snapshots/cifrado, caja, cortesías, CSRF y permisos.
- TypeScript/Vite build aprobado y cliente TS regenerado desde OpenAPI real. qrcode.react genera QR sin enviar credenciales a un servicio externo.
- Docker multietapa construido y preview F4 iniciada en http://localhost:5081, mismo origen para React/API, UID 1654. PostgreSQL y volumen de claves existentes preservados; contenedor F2 detenido y conservado.
- Playwright contra Docker: 12/12 escenarios de escritorio/móvil aprobados. Compra con rechazo/aprobación, diez QR y vale, recarga, historial, ofertas/cortesías y regresión F1/F2. Selectores F1 se limitaron a enlaces de workspace para distinguirlos de los nuevos eventos públicos. Capturas f4-order-{chromium,mobile}.png y f4-staff-{chromium,mobile}.png revisadas; sin desbordamiento a 390px.
- Reinicio real del contenedor: sesión persistida y credenciales exactas de diez tickets y vale recuperadas para los pedidos de ambos proyectos de navegador. Script reproducible tests/e2e/restart-benefits.mjs; archivos privados de comparación y sesión en .local, ignorados.

Ejecución: `./scripts/docker-demo.ps1`; reutilizar imagen con `-SkipBuild`. Detalles de reglas, API, guion y límites en [f3-f4.md](f3-f4.md). La preview usa Development restringido a localhost; no es un despliegue comercial. Las pruebas agregan datos reales de demostración locales y conservan ventas.

Límites: ingreso/canje y cámara esperan F5; informes F6; pagos reales y habilitación comercial F7. Todo pago es simulado; Demo/seed prohibidos en Production. Idempotencia y auditoría sin purga durante la demo. Todos los pagos posteriores a expiración van a revisión, sin readquisición automática. No se desplegó un servicio externo.

## Historial: entrega acumulada F0–F1

Solución .NET 10 Core/Infrastructure/API, React/TypeScript, PostgreSQL Compose, health/readiness, migraciones y Docker multietapa. Identity con login real, cookies seguras por entorno, CSRF, sesiones revocables persistidas y permisos de empresa/sede. Seed explícito e idempotente: dos empresas, tres sedes, administradores separados, seguridad/barra asignadas y cliente sin membresías. Contraseñas privadas por entorno, sin selector de rol ni secretos demo de producción.

## F2 entregada

- Crear, consultar y editar sedes, incluido aforo. Eliminar archiva sin cascadas; se protege una sede con eventos publicados y se limpian contextos de sesión al archivar.
- Varias plantillas por sede; SVG interactivo con paleta, selección, arrastre, posición, ancho/alto, rotación, capacidad, nombre y modo de venta. Panel usable con teclado y lista responsive.
- Guardado real de elementos/JSONB con revisión; reabrir conserva geometría. Cambios concurrentes devuelven 409.
- Publicación de versiones inmutables, numeradas y consultables. Publicar una revisión de nuevo conserva la misma versión.
- CRUD de borradores de evento con inicio/fin, ventanas de ingreso/canje y aforo; horas de Lima convertidas a UTC. Soporte de eventos que cruzan medianoche.
- Publicación transaccional solo con mapa publicado válido de esa sede y aforo suficiente. El evento guarda snapshot del mapa, sede y zona; recursos relacionales separados de geometría.
- Exclusive conserva capacidad humana y límite de asignación uno; Capacity conserva cupo; Decoration no crea recursos vendibles. Ninguna oferta/venta se implementó todavía.
- Agenda pública, detalle con mapa publicado y alternativa de lista. Los borradores no se exponen públicamente; mapas/eventos administrativos respetan membresía y asignación.
- Publicados de solo lectura; eventos anteriores conservan su versión cuando se modifica/republica la plantilla.

## Migraciones

1. `20261114122300_InitialIdentity`: Identity, empresas, sedes, membresías, asignaciones y sesiones.
2. `20261114123053_SessionContextIntegrity`: FK compuesta de contexto y check completo/nulo.
3. `20261114124115_VenueMapsAndEvents`: capacidad/archivo de sede; plantillas, elementos, versiones, eventos y recursos; JSONB, claves compuestas, checks y unicidad.

La tercera migración se aplicó sobre la base F1 existente y las bases vacías de prueba. Se corrigió antes de aplicar el default cero de aforo generado inicialmente por EF: las sedes existentes reciben 100, ajustable por administrador. Se preservaron los datos previos.

Aplicación explícita: cargar `scripts/dev-env.ps1` y ejecutar `dotnet run --project src/backend/ClubPlatform.Api -- --migrate`.

## Verificación ejecutada

- Backend build: aprobado, cero errores y advertencias. SDK 10.0.404, ASP.NET/EF 10.0.10, Npgsql EF 10.0.3.
- Frontend TypeScript/Vite build: aprobado. Contrato TS regenerado desde OpenAPI real; números JSON estrictos. React/TanStack/Router/Tailwind, Node 22.15.0.
- PostgreSQL 17.6 real: seis pruebas F2 aprobadas. Cubren snapshots, geometría persistida, independencia de sedes/empresas, permisos/CSRF, modos/capacidad, horarios nocturnos, conflictos simultáneos de guardado y publicaciones concurrentes sin duplicación, CRUD/archivo y FK contra otra sede.
- Regresión backend F0/F1: 13/13 aprobadas después de F2. Total: 19 pruebas backend aprobadas. La validación de configuración se volvió a comprobar después del último ajuste menor de validación: aprobada.
- Playwright con API/Vite: ocho escenarios aprobados (seis de identidad existentes y dos F2, escritorio/móvil). El flujo F2 crea sede/mapa, arrastra/redimensiona/rota, guarda y recarga, publica un evento nocturno y verifica snapshot tras una segunda publicación de plantilla.
- Capturas de editor y evento revisadas: `docs/screenshots/f2-editor-desktop.png`, `f2-editor-mobile.png`, `f2-event-chromium.png`, `f2-event-mobile.png`. Editor móvil comprobado sin desbordamiento horizontal a 390px.
- Docker multietapa F2: build aprobado. La comprobación final del contenedor se detalla al final de este archivo.
- Pruebas locales generan bases `clubtest_*` y las eliminan al terminar; la guarda rechaza hosts remotos. No SQLite/EF InMemory. No hubo despliegue cloud ni cobros.

## Uso y documentación

Instrucciones completas y guion F2 en `README.md`; reglas, API y decisiones en `docs/f2.md` y `docs/architecture.md`. Configuración privada estable en `.local/settings.json`; mismas cuentas de F1. Ejecutar setup, iniciar API cargando `dev-env.ps1 -Seed` y Vite en otra terminal.

Preview local: http://localhost:5081. PostgreSQL persistente en `infra_pgdata`; claves Data Protection en volumen `clubplatform_f1_keys`, reutilizado para preservar sesiones. Las E2E dejan sedes/eventos locales `E2E …` como datos reales de prueba; las versiones publicadas permanecen intactas.

## Límites registrados al terminar F2 (histórico)

F3 pendiente: ofertas/precios, inventario held/sold, reservas temporales, expiración e idempotencia de negocio. No se avanzó a F3. No hay ventas, pagos ni escáneres operativos en F2. Publicados son inmutables y no tienen flujo de cancelación comercial; se incorporará en su fase correspondiente. Supabase/Render, secretos cloud y habilitación de cobro no se han configurado.

HTTP y claves locales sin cifrado adicional son excepciones de Development documentadas. Demo/Production requieren HTTPS y almacenamiento persistente protegido. Seed/proveedor Demo se bloquean fuera de Development/Demo. Limpieza automática de sesiones y recuperación/MFA siguen pendientes según sus fases.

## Comprobación final de contenedor F2

Imagen `clubplatform:f2` y contenedor `clubplatform-f2-preview` en 5081, ejecutando como UID/GID 1654. Readiness aprobado y rutas SPA recargables. Los ocho E2E (identidad y flujo F2, escritorio/móvil) pasaron contra este contenedor y PostgreSQL persistente. La preview quedó iniciada para revisión.
