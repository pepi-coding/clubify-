using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Api;

public record ManualPaymentInfo(DateTimeOffset ExpiresAt, DateTimeOffset ServerTime, string? WhatsAppUrl, string Instructions, string Reference);
public record ReservationDto(Guid OrderId, Guid HoldId, string Status, long TotalCents, ManualPaymentInfo Payment);
public record ManualPaymentRequest(bool Approved, string Method, string? ReceiptReference, long AmountCents, DateTimeOffset? PaidAt, string Note);
public record PaymentSettingsRequest(string WhatsAppNumber, string Instructions);
public record PaymentQueueItem(Guid Id, string Reference, string CustomerEmail, string EventName, string Status, long TotalCents, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt, List<OrderLineDto> Lines, List<VerificationDto> Verifications);
public record VerificationDto(string Status, string? Method, string? ReceiptReference, long AmountCents, string? VerifiedBy, DateTimeOffset? PaidAt, DateTimeOffset VerifiedAt);
public sealed class VerifiedManualPaymentGateway : IPaymentGateway { public string Resolve(bool approved) => approved ? "Succeeded" : "Failed"; }
public sealed partial class CommerceService
{
    public async Task<ManualPaymentInfo> ManualInfo(ClubEvent e, Order order)
    {
        var settings = await db.Set<VenuePaymentSettings>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == e.TenantId && x.VenueId == e.VenueId);
        var hold = await db.Set<Hold>().SingleAsync(x => x.Id == order.HoldId);
        var deadline = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(hold.ExpiresAt, "America/Lima");
        var amount = (order.TotalCents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        var message = $"Hola, reservé para {e.Name}.\nReserva: {order.Id}\nImporte: S/ {amount}\nLímite de reserva: {deadline:dd/MM/yyyy HH:mm} (Lima).\nPagaré por Yape o Plin y enviaré la captura para verificar el abono. Confirmen mi reserva, por favor.";
        var url = settings == null || string.IsNullOrEmpty(settings.WhatsAppNumber) ? null : $"https://wa.me/{settings.WhatsAppNumber}?text={Uri.EscapeDataString(message)}";
        return new(hold.ExpiresAt, Now, url, settings?.Instructions ?? "Contacta a la sede para coordinar el pago.", order.Id.ToString());
    }
    public async Task<ReservationDto> ReserveForWhatsApp(ClubEvent e, string actor, List<OfferLineRequest> lines)
    {
        if (config["Payments:Provider"] != "WhatsAppManual") Fail(403, "MANUAL_PAYMENTS_DISABLED", "Las reservas por WhatsApp no están habilitadas.");
        if (!await db.Set<VenuePaymentSettings>().AnyAsync(x => x.TenantId == e.TenantId && x.VenueId == e.VenueId && x.WhatsAppNumber != "")) Fail(422, "WHATSAPP_NOT_CONFIGURED", "La sede debe configurar su número de WhatsApp antes de recibir reservas.");
        var h = await CreateHold(e, actor, lines, 60);
        var o = await CreateOrder(e, h.Id, actor, "WhatsAppManual");
        Audit(e, actor, "WhatsAppReservationCreated", o.Id, "Reserva por 60 minutos; pendiente de verificación de pago");
        return new(o.Id, h.Id, o.Status, o.TotalCents, await ManualInfo(e, o));
    }
    public async Task<CommerceResult> VerifyManualPayment(ClubEvent e, Guid id, string actor, ManualPaymentRequest r)
    {
        if (config["Payments:Provider"] != "WhatsAppManual") Fail(403, "MANUAL_PAYMENTS_DISABLED", "La verificación manual no está habilitada.");
        var order = await db.Set<Order>().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == e.TenantId && x.VenueId == e.VenueId && x.EventId == e.Id && x.Source == "WhatsAppManual");
        if (order == null) Fail(404, "ORDER_NOT_FOUND", "Reserva no encontrada.");
        if (r.Method is not ("Yape" or "Plin") || string.IsNullOrWhiteSpace(r.Note) || r.Note.Length > 500) Fail(422, "INVALID_VERIFICATION", "Indica Yape o Plin y una nota de verificación de hasta 500 caracteres.");
        if (r.Approved && (r.AmountCents != order!.TotalCents || string.IsNullOrWhiteSpace(r.ReceiptReference) || r.ReceiptReference.Trim().Length < 4 || r.ReceiptReference.Trim().Length > 80 || r.PaidAt == null || r.PaidAt > Now.AddMinutes(5) || r.PaidAt < order.CreatedAt.AddMinutes(-5))) Fail(422, "PAYMENT_DETAILS_REQUIRED", "El importe debe coincidir con la reserva. Registra código de operación (4–80 caracteres) y fecha/hora del abono.");
        if (order!.Status is "Confirmed" or "PaymentReview") return new(order.Id, order.Status, order.TotalCents);
        var reference = r.ReceiptReference?.Trim().ToUpperInvariant();
        if (r.Approved && await db.Set<PaymentAttempt>().AnyAsync(x => x.TenantId == e.TenantId && x.PaymentMethod == r.Method && x.ReceiptReference == reference)) Fail(409, "RECEIPT_ALREADY_USED", "Ese código de operación ya fue registrado para otro pago.");
        // A rejected screenshot does not consume a receipt reference and may be corrected within the original hour.
        var input = r.Approved ? r : r with { ReceiptReference = null, PaidAt = null };
        return await Pay(e, id, actor, r.Approved, new VerifiedManualPaymentGateway(), true, input);
    }
    public async Task<List<PaymentQueueItem>> ManualQueue(ClubEvent e)
    {
        var orders = await db.Set<Order>().Where(x => x.TenantId == e.TenantId && x.VenueId == e.VenueId && x.EventId == e.Id && x.Source == "WhatsAppManual").OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        var result = new List<PaymentQueueItem>();
        foreach (var o in orders) {
            var email = await db.Users.Where(x => x.Id == o.UserId).Select(x => x.Email!).SingleAsync();
            var hold = await db.Set<Hold>().SingleAsync(x => x.Id == o.HoldId);
            var lines = await db.Set<OrderLine>().Where(x => x.OrderId == o.Id).ToListAsync();
            var verifications = await db.Set<PaymentAttempt>().Where(x => x.OrderId == o.Id).OrderByDescending(x => x.CreatedAt).Select(x => new VerificationDto(x.Status, x.PaymentMethod, x.ReceiptReference, x.AmountCents, x.VerifiedBy, x.PaidAt, x.CreatedAt)).ToListAsync();
            result.Add(new(o.Id, o.Id.ToString(), email, e.Name, o.Status, o.TotalCents, hold.ExpiresAt, o.CreatedAt, lines.Select(x => new OrderLineDto(x.Name, x.Quantity, x.UnitPriceCents, x.Admissions, x.Zone, System.Text.Json.JsonSerializer.Deserialize<List<DrinkComponent>>(x.DrinksJson, Json)!)).ToList(), verifications));
        }
        return result;
    }
}
public static class ManualPaymentEndpoints
{
    private static string Actor(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static async Task<IResult> Run<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (CommerceException ex) { return Results.Problem(statusCode: ex.Status, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code, ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() }); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_PaymentAttempt_TenantId_PaymentMethod_ReceiptReference" }) { return Results.Problem(statusCode: 409, title: "Ese código de operación ya fue registrado para otro pago."); }
    }
    private static void Scope(ClubEvent e, Guid t, Guid v) { if (e.TenantId != t || e.VenueId != v) throw new CommerceException(404, "EVENT_NOT_FOUND", "Evento no encontrado."); }
    public static void MapManualPayments(this WebApplication app)
    {
        app.MapPost("/api/v1/reservations", (HoldRequest r, HttpContext c, CommerceService s) => Run(() => s.Transaction(r.EventId, e => s.ReserveForWhatsApp(e, Actor(c), r.Lines), Actor(c), "whatsapp-reservation", c.Request.Headers["Idempotency-Key"].ToString(), r))).RequireAuthorization().RequireRateLimiting("commerce").Produces<ReservationDto>();
        var venue = app.MapGroup("/api/v1/tenants/{tenantId:guid}/venues/{venueId:guid}");
        venue.MapGet("/payment-settings", async (Guid tenantId, Guid venueId, AppDbContext db) => Results.Ok(await db.Set<VenuePaymentSettings>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.VenueId == venueId) ?? new VenuePaymentSettings { TenantId = tenantId, VenueId = venueId })).RequireAuthorization("VenueAdmin").Produces<VenuePaymentSettings>();
        venue.MapPut("/payment-settings", (Guid tenantId, Guid venueId, PaymentSettingsRequest r, AppDbContext db) => Run(async () => {
            var number = Regex.Replace(r.WhatsAppNumber?.Trim() ?? "", @"[+\s()\-]", "");
            if (number.Length > 0 && !Regex.IsMatch(number, @"^[1-9][0-9]{7,14}$") || string.IsNullOrWhiteSpace(r.Instructions) || r.Instructions.Length > 1000) throw new CommerceException(422, "INVALID_PAYMENT_SETTINGS", "Usa un número internacional de 8–15 dígitos e instrucciones de hasta 1000 caracteres.");
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Venues.FromSqlInterpolated($"SELECT * FROM app.\"Venues\" WHERE \"TenantId\" = {tenantId} AND \"Id\" = {venueId} FOR UPDATE").SingleAsync();
            var settings = await db.Set<VenuePaymentSettings>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.VenueId == venueId);
            if (settings == null) { settings = new() { TenantId = tenantId, VenueId = venueId }; db.Add(settings); }
            settings.WhatsAppNumber = number; settings.Instructions = r.Instructions.Trim(); await db.SaveChangesAsync(); await tx.CommitAsync(); return settings;
        })).RequireAuthorization("VenueAdmin").Produces<VenuePaymentSettings>();
        venue.MapGet("/events/{eventId:guid}/manual-payments", (Guid tenantId, Guid venueId, Guid eventId, CommerceService s) => Run(() => s.Transaction(eventId, e => { Scope(e, tenantId, venueId); return s.ManualQueue(e); }))).RequireAuthorization("PaymentVerifier").RequireRateLimiting("commerce").Produces<List<PaymentQueueItem>>();
        venue.MapPost("/events/{eventId:guid}/manual-payments/{orderId:guid}/verify", (Guid tenantId, Guid venueId, Guid eventId, Guid orderId, ManualPaymentRequest r, HttpContext c, CommerceService s) => Run(() => s.Transaction(eventId, e => { Scope(e, tenantId, venueId); return s.VerifyManualPayment(e, orderId, Actor(c), r); }, Actor(c), "manual-verification", c.Request.Headers["Idempotency-Key"].ToString(), new { orderId, request = r }))).RequireAuthorization("PaymentVerifier").RequireRateLimiting("commerce").Produces<CommerceResult>();
    }
}
