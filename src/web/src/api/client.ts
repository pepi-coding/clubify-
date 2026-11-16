import createClient from "openapi-fetch";
import type { paths } from "./schema";
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string,
    public recordedAt?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}
export const apiClient = createClient<paths>({ baseUrl: "" });
apiClient.use({
  async onRequest({ request }) {
    if (!["GET", "HEAD", "OPTIONS"].includes(request.method)) {
      const response = await fetch("/api/v1/auth/csrf", {
        signal: request.signal,
      });
      if (!response.ok)
        throw new ApiError(
          "No se pudo obtener protección de sesión.",
          response.status,
        );
      request.headers.set("X-CSRF-TOKEN", (await response.json()).token);
    }
    return request;
  },
  async onResponse({ response }) {
    if (response.ok) return response;
    const problem = await response
      .clone()
      .json()
      .catch(() => null);
    const messages: Record<number, string> = {
      401: "Inicia sesión para continuar.",
      403: "No tienes permiso para esta operación.",
      404: "No se encontró el recurso.",
    };
    const message =
      messages[response.status] ??
      ([400, 409, 422, 429].includes(response.status)
        ? Object.values(problem?.errors ?? {})
            .flat()
            .join(" ") ||
          problem?.title ||
          "Revisa los datos de la operación."
        : "No se pudo confirmar la operación. Revisa los datos y la conexión.");
    throw new ApiError(
      message,
      response.status,
      problem?.code,
      problem?.recordedAt,
    );
  },
});
