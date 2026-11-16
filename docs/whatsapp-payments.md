# Pagos manuales por WhatsApp — MVP

Cambio solicitado por el usuario el 14/11/2026, antes de F5. Sustituye el checkout simulado de nuevas reservas online. No avanza a otras fases.

## Flujo del cliente

1. Seleccionar una oferta y pulsar Reservar.
2. Una transacción crea hold de 60 minutos y pedido WhatsAppManual, con precio/composición calculados por servidor. No emite pases.
3. El navegador abre WhatsApp con número de la sede, referencia del pedido, evento, importe y vencimiento en Lima. Se usa click-to-chat oficial (`wa.me`); el mensaje se prepara, no se envía automáticamente. La captura se adjunta y envía por el cliente dentro de WhatsApp.
4. Cliente paga por Yape o Plin según las instrucciones del negocio y envía comprobante. En Mis pedidos ve el estado, plazo y botón para volver a WhatsApp.
5. Promotor contrasta el comprobante con el abono real en su cuenta y registra la verificación. Solo entonces se confirma la reserva y aparecen los pases/vale.

No hay integración con APIs de Yape, Plin ni WhatsApp Business. La aplicación no recibe ni analiza capturas. No deduce un pago por abrir WhatsApp, enviar una imagen o pulsar un botón del cliente. Verificación humana explícita y auditada.

## Configuración y permisos

AdminA → Miraflores → Reservas y pagos por WhatsApp → Configurar WhatsApp de pagos. Guardar número internacional, por ejemplo con +51 para Perú, e instrucciones del negocio. No se asignó un número real sin que el usuario lo facilite. Número vacío suspende nuevas reservas por WhatsApp; no impide consultar pedidos ni registrar la revisión de pagos existentes.

Nuevo rol técnico Promoter, presentado como Promotor en su panel; membresía por empresa y asignación por sede. Seed opcional de `promoter@demo.local` con `Seed__Passwords__Promoter`. dev-env genera/persiste esa contraseña local una sola vez, igual que las demás cuentas. Admin/manager también pueden verificar; Security, BarStaff, Cashier y clientes no pueden. El promotor no modifica WhatsApp, sedes, ofertas ni cortesías.

Política PaymentVerifier comprueba empresa y sede en cada ruta. Lectura de cola devuelve como máximo los últimos 100 pedidos de ese evento y datos mínimos para verificar, sin credenciales QR. Cliente solo consulta sus propios pedidos. Configuración del número solo para VenueAdmin. CSRF en todas las mutaciones, rate limit e Idempotency-Key en reserva/verificación.

## Verificación

Requiere Yape o Plin, nota y decisión. Aprobación exige importe exacto en céntimos, código de operación de 4–80 caracteres y fecha/hora del abono; no permite horas futuras ni anteriores a la creación del pedido más un margen de cinco minutos. El operador confirma en UI que revisó el abono en su cuenta.

Rechazo registra intento Failed y auditoría, sin pases ni liberación anticipada; el cliente puede corregir el comprobante dentro de la hora original. No extiende la reserva.

Aprobación dentro de plazo convierte held→sold y emite tickets/vale con la misma transacción y restricciones únicas de F4. Reintentos y verificaciones concurrentes producen una sola emisión. Código aprobado normalizado y único por empresa/método impide reutilizarlo para otro pedido; un rechazo no consume ese código.

El plazo de una hora es para la reserva completa. Si el promotor verifica después de vencer, el pago se registra como Succeeded pero el pedido queda PaymentReview, sin pases ni reapropiar inventario. Esto también aplica si el cliente envió la captura antes del vencimiento y el operador la revisó después. No existe devolución automática; el negocio resuelve la incidencia. No confirmar manualmente un pedido nuevo para ocultar ese caso.

La reserva y verificación usan el bloqueo transaccional por evento/inventarios, ledger, worker persistente y claves de cifrado existentes. WhatsApp y recibos no son credenciales de acceso. Las referencias de pago no se incluyen en logs de aplicación ni tokens en auditoría.

## Cambios y ejecución

Migración `20261114191028_WhatsAppManualPayments`: configuración de pagos por sede, ampliación del check de roles, método/código/operador/fecha de abono y unicidad de comprobantes. Datos F0–F4 preservados.

Development y `scripts/docker-demo.ps1` configuran Payments:Provider=WhatsAppManual. DemoPayment sigue como adaptador de pruebas aisladas y permanece prohibido en Production; el endpoint de autoaprobación demo no funciona en modo WhatsAppManual. Las cortesías siguen disponibles a administradores por ser emisiones sin cobro. Caja demo solo funciona en modo Demo.

Ejecutar `./scripts/docker-demo.ps1` para construir, migrar y actualizar http://localhost:5081. Mismos volúmenes PostgreSQL y claves. No hubo despliegue externo ni envío de mensajes. Esta entrega no completa los restantes requisitos comerciales de F7.

Verificación reproducible: suite backend PostgreSQL y Playwright. El E2E configura temporalmente un número ficticio, intercepta la navegación a wa.me sin contacto externo y restaura la configuración original incluso al fallar. Comprueba escritorio/móvil, rechazo/corrección y diez QR más vale después de aprobación del promotor. Capturas: whatsapp-order-{chromium,mobile}.png y whatsapp-promoter-{chromium,mobile}.png. Los registros/sesiones de comparación de reinicio permanecen en .local (ignorado).

Formato oficial de enlaces: https://faq.whatsapp.com/5913398998672934
