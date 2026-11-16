import createClient from "openapi-fetch";
import type { paths } from "./schema";
export const apiClient = createClient<paths>({ baseUrl: "" });
apiClient.use({
  async onRequest({ request }) {
    if (!["GET", "HEAD", "OPTIONS"].includes(request.method)) {
      const response = await fetch("/api/v1/auth/csrf");
      if (!response.ok) throw Error("No se pudo obtener protección de sesión.");
      request.headers.set("X-CSRF-TOKEN", (await response.json()).token);
    }
    return request;
  },
  async onResponse({ response }) {
    if (response.ok) return response;
    if (response.status === 401) throw Error("Inicia sesión para continuar.");
    if (response.status === 403)
      throw Error("No tienes permiso para esta operación.");
    if (response.status === 404) throw Error("No se encontró el recurso.");
    if ([400, 409, 422, 429].includes(response.status)) {
      const problem = await response
        .clone()
        .json()
        .catch(() => null);
      if (problem)
        throw Error(
          Object.values(problem.errors ?? {})
            .flat()
            .join(" ") ||
            problem.title ||
            "Revisa los datos de la operación.",
        );
    }
    throw Error(
      "No se pudo confirmar la operación. Revisa los datos y la conexión.",
    );
  },
});

