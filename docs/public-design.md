# Presentación pública NOCHE

La agenda y el detalle público usan el kit `noche-assets-ambos-estilos.zip`: fotografías ilustrativas generadas con IA, logos provisionales NOCHE, tokens CSS y piezas SVG originales. Los archivos están en `src/web/public/assets/noche`. No se usan los eventos, precios ni reservas ficticios del comparador.

Electric Night es el tema inicial. El selector permite cambiar a Midnight Lounge y guarda la preferencia en el navegador. Esta preferencia **no es una configuración de empresa**. La carga de logos, portadas y temas por empresa queda para una siguiente entrega; no se añadieron endpoints ni cambios de base de datos.

La agenda filtra eventos publicados por identificador de sede y fecha de Lima, sin excluir nombres E2E. Las tarjetas no anuncian precios inventados. Las fotos actuales son de demostración y no representan los locales reales.

El mapa conserva las coordenadas, dimensiones y rotación de cada elemento publicado. Añade decoración SVG por tipo, estados con texto y borde, selección con teclado y una lista alternativa. Solo se seleccionan recursos disponibles con ofertas reales. El editor y las pantallas de trabajo conservan su presentación operativa.

La selección del mapa filtra ofertas; el selector de oferta permite elegir entre las versiones devueltas por el catálogo. Precio, accesos, bebidas, inventario y minutos de reserva proceden de la API. El backend mantiene la validación final. La reserva conserva la idempotencia, la pestaña separada de WhatsApp y el flujo de revisión manual Yape/Plin.

No hay migraciones, reinicios de datos ni despliegue en esta entrega. El contenedor de Docker existente no se reemplazó. Para incorporarlo al contenedor local se puede ejecutar el script habitual `./scripts/docker-demo.ps1` cuando se decida actualizar esa preview.

Verificación: build TypeScript/Vite; Playwright en escritorio y móvil para temas, filtros, geometría guardada, selección accesible, vendido/sin ofertas y errores de red, además del flujo existente de reserva, pestaña WhatsApp y confirmación por promotor. Capturas locales en `docs/screenshots/noche-*.png` (ignoradas por Git).
