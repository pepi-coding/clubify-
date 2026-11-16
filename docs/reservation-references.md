# Referencias cortas de reserva

Cambio del 16/11/2026: la referencia visible de pedidos/reservas usa ocho caracteres agrupados, por ejemplo `K7M4-P9TX`. Alfabeto `23456789ABCDEFGHJKLMNPQRSTUVWXYZ`, sin 0, 1, I ni O. Un código por pedido, globalmente único en esta base de datos y persistido para todos sus estados. No se regenera al pagar, cancelar, expirar ni reiniciar.

Los GUID siguen siendo las claves internas y los IDs de rutas API. El código es una referencia operativa, no una credencial de acceso: sesión, permisos y QR opacos de 256 bits conservan sus reglas. No se introduce una consulta pública de pedidos por código.

PostgreSQL genera el valor por defecto mediante `app.next_order_reference()` y una secuencia no cíclica (`OrderReferenceNumbers`). Permutación biyectiva sobre 2^40 valores y codificación base32 de ancho fijo; el multiplicador impar asegura unicidad sin reintentos aleatorios. La secuencia es atómica entre conexiones/réplicas. Check de formato e índice único refuerzan la regla. Un rollback puede dejar un salto de secuencia; nunca se reutiliza un código al cancelar una reserva. La propiedad es de solo creación en EF.

Migración `20261116165758_ShortReservationReferences`: crea secuencia/función, asigna códigos a los pedidos existentes sin cambiar IDs, importe, estado, inventario ni QR, e instala check/unicidad. Actualiza los resultados idempotentes ya guardados de reservas WhatsApp y operaciones de pedido/pago para que un reintento anterior muestre la nueva referencia y el enlace correcto. Los hashes de petición y efectos comerciales no cambian. Conservar datos y secuencia en los backups; no resetearla ni degradar esta migración sobre reservas comunicadas al cliente.

Se muestra la referencia en Mis pedidos, detalle, instrucciones de pago, WhatsApp, cola del promotor y confirmación de ventas/cortesías. El promotor puede buscar sin guion, con espacios o en minúsculas; también se conservan búsquedas por GUID para mensajes antiguos que ya se enviaron y no pueden modificarse.

Verificación: generación concurrente desde dos conexiones PostgreSQL (512 códigos), unicidad/rechazo de duplicados en DB, estabilidad en detalle/historial y reconstrucción de una base con esquema previo para probar backfill, conservación de tickets/vale y replay del enlace WhatsApp. La prueba de navegador comprueba la referencia corta en el mensaje y búsqueda del promotor sin guion/minúsculas. Los endpoints conservan IDs internos; QR de acceso/consumo no se sustituyen por estos códigos.

Resultado final: 40/40 backend y 16/16 navegador aprobados. Reinicio real conserva referencia, sesiones, timestamp de ingreso, saldo parcial y credenciales QR. Los 48 pedidos existentes en la DB local recibieron 48 referencias únicas sin faltantes.
