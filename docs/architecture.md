# Arquitectura y decisiones F0–F5

Fecha: 14/11/2026. Alcance autorizado acumulado: F0–F4. Decisiones de inventario, pagos y emisión: [f3-f4.md](f3-f4.md).

F5 añadida el 16/11/2026: admisiones de un uso, canje parcial posterior a ingreso, paneles separados Security/BarStaff, cámara con fallback manual, logs/auditoría e idempotencia con recuperación del propio operador. Bloqueos y relaciones compuestas sobre PostgreSQL; no se confirma una acción por leer QR ni por tener conectividad local. Detalles: [f5.md](f5.md).

Referencias de reserva: Order.Reference se genera y persiste en PostgreSQL con secuencia no cíclica, permutación/base32 y unicidad global en esta DB. Formato XXXX-XXXX sin caracteres 0/1/I/O. No sustituye PK/FK GUID ni tokens QR y no concede autorización. Migración con backfill y actualización de respuestas idempotentes: [reservation-references.md](reservation-references.md).

Cambio posterior del MVP: nuevas reservas por WhatsApp con una hora de vigencia y verificación humana de Yape/Plin por Promotor de sede. Reutiliza transacciones de inventario/emisión e introduce configuración de WhatsApp por sede, datos auditados de verificación y unicidad del código de operación. Ver [whatsapp-payments.md](whatsapp-payments.md), que sustituye las reglas anteriores del checkout online.

## Componentes

- Core: empresas, sedes, membresías por empresa, asignaciones por sede y sesiones web. Customer es la condición de una cuenta sin permisos de personal; nunca se crea una membresía por autenticación o compra.
- Infrastructure: ASP.NET Core Identity, contexto EF/Npgsql, migraciones PostgreSQL y seed transaccional protegido por advisory lock PostgreSQL. La extensión de Identity `IsPlatformAdmin` se reserva para administración de plataforma, independiente de membresías.
- API: cookies, revocación persistida, antiforgery, políticas Staff/VenueAdmin/Platform, DTOs OpenAPI, errores Problem Details y health. Toda consulta empresarial tiene tenant explícito y toda ruta de sede pasa por la misma autorización.
- Web: React Router, TanStack Query, Tailwind y cliente openapi-fetch con esquema TypeScript generado desde OpenAPI. Vite usa proxy para compartir origen durante desarrollo; Docker sirve el build desde ASP.NET.

## Aislamiento

`Venues` tiene clave alternativa `(TenantId, Id)`. Asignaciones referencian esa clave y `(TenantId, UserId)` de la membresía; una sede ajena no puede conectarse con un empleado de otra empresa. El contexto opcional de sesión también referencia la sede mediante clave compuesta y exige ambos IDs juntos. Eliminación de relaciones organizativas restringida.

Las políticas buscan membresía activa por usuario autenticado y tenant de ruta. TenantAdmin opera todas sus sedes; los otros roles necesitan asignación específica. VenueManager puede guardar el nombre de una sede asignada; Cashier/Security/BarStaff solo consultan su contexto F1. El cliente nunca tiene permisos administrativos. Las consultas no usan un tenant suministrado por UI sin validarlo; la selección guardada no concede permisos.

Se devuelve 404 para tenant o sede ajena y 403 para permisos insuficientes en una empresa conocida. PlatformAdmin tiene endpoint de listado de empresas; no recibe acceso implícito a datos de operación. El alta de plataforma no es un endpoint público ni se incluye en las cuentas demo.

## Sesiones y secretos

Identity verifica contraseñas y lockout. Al iniciar sesión se crea `WebSession` con UUID y expiración de ocho horas. El UUID va dentro de la cookie cifrada, no es un bearer token público. Cada solicitud autentica contra la sesión persistida y comprueba security stamp y permiso de plataforma actuales. Logout revoca en DB antes de borrar la cookie; una copia capturada no revive la sesión.

CSRF se comprueba en cada mutación `/api`, incluido login. La UI pide un token nuevo para cada mutación; la cookie de CSRF es HttpOnly. Development permite HTTP local; Demo/Production requieren Secure. Cookies SameSite Strict. No localStorage de credenciales.

Claves Data Protection en una ruta persistente configurada explícitamente; no crear claves en disco efímero al desplegar. Para desarrollo, el script de inicialización genera secretos una vez y guarda `.local/settings.json`; el proceso recibe variables de entorno. La ubicación local de las claves de esta sesión se conserva en ese archivo. Respaldar configuración y claves juntas y restringir acceso. No hay credenciales cloud.

## Base y despliegue

PostgreSQL 17.6 local fijado; todavía no hay proyecto Supabase seleccionado. La compatibilidad cloud se verificará al seleccionar su versión mayor en F6, sin afirmar despliegue probado. EF Core 10.0.10/Npgsql EF 10.0.3; SDK 10.0.404 y runtime 10.0.10. Lockfiles NuGet/npm comprometibles. Microsoft.OpenApi 2.7.5 se fija para evitar la vulnerabilidad reportada para 2.0.0.

Migraciones aplicadas mediante tarea explícita `--migrate`; servir HTTP no ejecuta migraciones. Readiness falla con DB caída o migraciones pendientes. Liveness solo informa que el proceso vive. Proveedor de pago Disabled por defecto; Demo y el seed provocan fallo fuera de Development/Demo. No hay adaptador de pagos funcional hasta F4.

Docker multietapa ejecuta sin root, sirve SPA y API en el mismo origen y permite recarga de rutas; `/api/*` no cae al HTML. La publicación y configuración de proxy/secretos cloud pertenecen a F6.

## Límites

El seed aprovisiona las empresas/cuentas solicitadas; no se ofrece CRUD genérico de permisos ni registro público en F1. Mapas y eventos se incorporaron en F2 (ver `f2.md`). F3/F4 incorporan ofertas, inventario relacional, holds, pedidos, pagos simulados, QR cifrados, auditoría e idempotencia persistida (ver `f3-f4.md`). Las sesiones se conservan para revocación; limpieza automática pendiente, con retención propuesta de 30 días después de vencimiento/revocación. No se presenta el producto como apto para cobrar dinero real.

Referencia técnica utilizada para CSRF: [documentación oficial ASP.NET Core 10](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0). Referencia de actualización OpenAPI: [aviso de seguridad del proyecto](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc).

