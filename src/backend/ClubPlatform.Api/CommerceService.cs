using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Api;

public record OfferLineRequest(Guid OfferVersionId, int Quantity);
public record HoldRequest(Guid EventId, List<OfferLineRequest> Lines);
public record OrderRequest(Guid HoldId);
public record DemoPaymentRequest(bool Approved);
public record StaffSaleRequest(string CustomerEmail, List<OfferLineRequest> Lines, string Reason, bool Approved = true);
public record OfferSave(Guid? OfferId, string Name, long PriceCents, Guid? ResourceId, int ResourceUnits, int Admissions, List<DrinkInput> Drinks, int ExpectedRevision = 0);
public record DrinkInput(Guid ProductId, int Quantity);
public record ProductSave(string Name);
public record CommerceResult(Guid Id, string Status, long TotalCents, DateTimeOffset? ExpiresAt = null);
public record OfferDto(Guid Id, Guid OfferId, int Number, string Name, long PriceCents, string Currency, Guid? ResourceId, int ResourceUnits, int Admissions, List<DrinkComponent> Drinks);
public record InventoryDto(Guid Id, Guid? ResourceId, string Name, int Limit, int Held, int Sold, int Available, string State);
public record CatalogDto(Guid EventId, DateTimeOffset ServerTime, List<OfferDto> Offers, List<InventoryDto> Inventory, string PaymentMode = "Demo", int ReservationMinutes = 10, bool PaymentsConfigured = true);
public record HoldDto(Guid Id, Guid EventId, string Status, long TotalCents, DateTimeOffset ExpiresAt, DateTimeOffset ServerTime, List<OfferLineRequest> Lines);
public record TicketDto(Guid Id, int Ordinal, string Zone, string Status, string Token);
public record EntitlementDto(string Name, int Granted, int Delivered);
public record VoucherDto(Guid Id, string Token, List<EntitlementDto> Lines);
public record OrderLineDto(string Name, int Quantity, long UnitPriceCents, int Admissions, string Zone, List<DrinkComponent> Drinks);
public record OrderDto(Guid Id, Guid EventId, string EventName, string Status, string Source, long TotalCents, string Currency, DateTimeOffset CreatedAt, List<OrderLineDto> Lines, List<TicketDto> Tickets, VoucherDto? Voucher, List<string> PaymentStatuses, ManualPaymentInfo? Payment = null);
public sealed class CommerceException(int status, string code, string message) : Exception(message) { public int Status { get; } = status; public string Code { get; } = code; }

public sealed partial class CommerceService(AppDbContext db, IDataProtectionProvider protection, IConfiguration config, TimeProvider clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTimeOffset Now => clock.GetUtcNow();
    private readonly IDataProtector protector = protection.CreateProtector("ClubPlatform.Benefits.v1");
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static void Fail(int status, string code, string message) => throw new CommerceException(status, code, message);
    private static T Scope<T>(T entity, ClubEvent e) where T : EventEntity { entity.TenantId = e.TenantId; entity.VenueId = e.VenueId; entity.EventId = e.Id; return entity; }
    public async Task<T> Transaction<T>(Guid eventId, Func<ClubEvent, Task<T>> action, string? actor = null, string? operation = null, string? key = null, object? body = null, Guid? expectedTenant = null, Guid? expectedVenue = null)
    {
        if (operation != null && (string.IsNullOrWhiteSpace(key) || key.Length > 100)) Fail(422, "IDEMPOTENCY_REQUIRED", "Idempotency-Key de 1 a 100 caracteres requerido.");
        await using var tx = await db.Database.BeginTransactionAsync();
        if (operation != null) {
            var lockId = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(actor + ":" + operation + ":" + key)), 0);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})");
        }
        var e = await db.Events.FromSqlInterpolated($"SELECT * FROM app.\"Events\" WHERE \"Id\" = {eventId} FOR UPDATE").SingleOrDefaultAsync();
        if (e == null || e.Status != "Published") { Fail(404, "EVENT_NOT_FOUND", "Evento publicado no encontrado."); }
        if ((expectedTenant != null && e!.TenantId != expectedTenant) || (expectedVenue != null && e!.VenueId != expectedVenue)) Fail(404, "EVENT_NOT_FOUND", "Evento no encontrado.");
        var fingerprint = Hash(JsonSerializer.Serialize(body, Json));
        if (operation != null) {
            var prior = await db.Set<IdempotencyRecord>().SingleOrDefaultAsync(x => x.ActorId == actor && x.TenantId == e!.TenantId && x.Operation == operation && x.Key == key);
            if (prior != null) { if (prior.BodyHash != fingerprint) Fail(409, "IDEMPOTENCY_CONFLICT", "La clave ya se utilizó con otros datos."); await tx.CommitAsync(); return JsonSerializer.Deserialize<T>(prior.ResultJson, Json)!; }
        }
        await EnsureInventory(e!); await Expire(e!);
        var result = await action(e!);
        if (operation != null) db.Add(Scope(new IdempotencyRecord { ActorId = actor!, Operation = operation, Key = key!, BodyHash = fingerprint, ResultJson = JsonSerializer.Serialize(result, Json), CreatedAt = Now }, e!));
        await db.SaveChangesAsync(); await tx.CommitAsync(); return result;
    }
    private async Task EnsureInventory(ClubEvent e)
    {
        if (!await db.Set<EventInventory>().AnyAsync(x => x.EventId == e.Id)) {
            db.Add(Scope(new EventInventory { Limit = e.Capacity }, e));
            foreach (var r in await db.EventResources.Where(x => x.EventId == e.Id && x.TenantId == e.TenantId).OrderBy(x => x.Id).ToListAsync()) db.Add(Scope(new EventInventory { ResourceId = r.Id, Limit = r.AllocationLimit }, e));
            await db.SaveChangesAsync();
        }
        // The event row is the common lock, followed by all its inventory rows in UUID order.
        await db.Set<EventInventory>().FromSqlInterpolated($"SELECT * FROM app.\"EventInventory\" WHERE \"EventId\" = {e.Id} ORDER BY \"Id\" FOR UPDATE").ToListAsync();
    }
    public async Task Expire(ClubEvent e)
    {
        foreach (var hold in await db.Set<Hold>().Where(x => x.EventId == e.Id && x.Status == "Active" && x.ExpiresAt <= Now).ToListAsync()) {
            await Release(hold, "Expired");
            var order = await db.Set<Order>().SingleOrDefaultAsync(x => x.HoldId == hold.Id);
            if (order?.Status == "PendingPayment") order.Status = "Expired";
        }
        await db.SaveChangesAsync();
    }
    private async Task Release(Hold hold, string status)
    {
        foreach (var a in await db.Set<InventoryAllocation>().Where(x => x.HoldId == hold.Id && x.Status == "Held").ToListAsync()) {
            var i = await db.Set<EventInventory>().SingleAsync(x => x.Id == a.InventoryId); i.Held -= a.Quantity; a.Status = "Released";
        }
        hold.Status = status;
    }
    public async Task<CatalogDto> Catalog(ClubEvent e, bool admin = false)
    {
        var offers = await db.Set<OfferVersion>().Where(x => x.EventId == e.Id && db.Set<Offer>().Any(o => o.Id == x.OfferId && !o.Archived && o.Revision == x.Number)).OrderBy(x => x.Name).ToListAsync();
        var resources = await db.EventResources.Where(x => x.EventId == e.Id).ToListAsync();
        var inventory = await db.Set<EventInventory>().Where(x => x.EventId == e.Id).OrderBy(x => x.Id).ToListAsync();
        return new(e.Id, Now, offers.Select(x => new OfferDto(x.Id, x.OfferId, x.Number, x.Name, x.PriceCents, x.Currency, x.ResourceId, x.ResourceUnits, x.Admissions, JsonSerializer.Deserialize<List<DrinkComponent>>(x.DrinksJson, Json)!)).ToList(), inventory.Select(x => new InventoryDto(x.Id, x.ResourceId, x.ResourceId == null ? "Aforo total" : resources.Single(r => r.Id == x.ResourceId).Name, x.Limit, x.Held, x.Sold, x.Blocked ? 0 : x.Limit - x.Held - x.Sold, x.Blocked ? "Blocked" : x.Sold == x.Limit ? "Sold" : x.Held + x.Sold == x.Limit ? "Reserved" : "Available")).ToList(), config["Payments:Provider"] ?? "Disabled", config["Payments:Provider"] == "WhatsAppManual" ? 60 : 10, config["Payments:Provider"] != "WhatsAppManual" || await db.Set<VenuePaymentSettings>().AnyAsync(x => x.TenantId == e.TenantId && x.VenueId == e.VenueId && x.WhatsAppNumber != ""));
    }
    public async Task<OfferDto> SaveOffer(ClubEvent e, OfferSave r, string actor)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 120 || r.PriceCents < 0 || r.PriceCents > 100000000 || r.Admissions < 0 || r.Admissions > 1000 || r.ResourceUnits < 0 || r.ResourceUnits > 1000 || r.Drinks == null || r.Drinks.Count > 20 || r.Drinks.Any(x => x.Quantity < 1 || x.Quantity > 100) || r.Drinks.Select(x => x.ProductId).Distinct().Count() != r.Drinks.Count) Fail(422, "INVALID_OFFER", "Revisa nombre, precio, cantidades y composición.");
        var resource = r.ResourceId == null ? null : await db.EventResources.SingleOrDefaultAsync(x => x.Id == r.ResourceId && x.EventId == e.Id && x.TenantId == e.TenantId);
        if (r.ResourceId != null && resource == null) Fail(404, "RESOURCE_NOT_FOUND", "Recurso no encontrado.");
        if (resource == null ? r.ResourceUnits != 0 || r.Admissions != 0 : resource.Mode == "Exclusive" ? r.ResourceUnits != 1 || r.Admissions != resource.HumanCapacity : r.ResourceUnits < 1 || r.Admissions != r.ResourceUnits) Fail(422, "INVALID_REQUIREMENTS", "Box: una unidad y todos sus accesos. Zona: unidades iguales a accesos. Bebidas: sin accesos.");
        if (r.Admissions == 0 && r.Drinks!.Count == 0) Fail(422, "EMPTY_OFFER", "La oferta debe conceder un beneficio.");
        var drinks = new List<DrinkComponent>();
        foreach (var d in r.Drinks!) { var p = await db.Set<Product>().SingleOrDefaultAsync(x => x.Id == d.ProductId && x.TenantId == e.TenantId && !x.Archived); if (p == null) Fail(404, "PRODUCT_NOT_FOUND", "Producto no encontrado."); drinks.Add(new(p!.Id, p.Name, d.Quantity)); }
        var offer = r.OfferId == null ? Scope(new Offer(), e) : await db.Set<Offer>().SingleOrDefaultAsync(x => x.Id == r.OfferId && x.EventId == e.Id && x.TenantId == e.TenantId);
        if (offer == null) Fail(404, "OFFER_NOT_FOUND", "Oferta no encontrada.");
        if (offer!.Revision != r.ExpectedRevision) Fail(409, "REVISION_CONFLICT", "La oferta cambió. Recarga antes de guardar.");
        if (r.OfferId == null) db.Add(offer); offer.Name = r.Name.Trim(); offer.Revision++; offer.Archived = false;
        var version = Scope(new OfferVersion { OfferId = offer.Id, Number = offer.Revision, Name = offer.Name, PriceCents = r.PriceCents, ResourceId = r.ResourceId, ResourceUnits = r.ResourceUnits, Admissions = r.Admissions, DrinksJson = JsonSerializer.Serialize(drinks, Json), CreatedAt = Now }, e); db.Add(version); Audit(e, actor, "OfferVersionCreated", version.Id, "");
        return new(version.Id, offer.Id, version.Number, version.Name, version.PriceCents, "PEN", version.ResourceId, version.ResourceUnits, version.Admissions, drinks);
    }
    public async Task<Hold> CreateHold(ClubEvent e, string buyer, List<OfferLineRequest> lines, int? holdMinutes = null)
    {
        if (e.EndsAt <= Now) Fail(409, "EVENT_ENDED", "El evento ya terminó.");
        if (lines == null || lines.Count < 1 || lines.Count > 20 || lines.Any(x => x.Quantity < 1 || x.Quantity > 100) || lines.Select(x => x.OfferVersionId).Distinct().Count() != lines.Count) Fail(422, "INVALID_LINES", "Selecciona de 1 a 20 ofertas, cantidades de 1 a 100, sin duplicados.");
        var inventory = await db.Set<EventInventory>().Where(x => x.EventId == e.Id).ToListAsync(); var needs = new Dictionary<Guid, int>(); var selected = new List<(OfferVersion v, int q)>(); long total = 0;
        foreach (var line in lines!) {
            var v = await db.Set<OfferVersion>().SingleOrDefaultAsync(x => x.Id == line.OfferVersionId && x.EventId == e.Id && x.TenantId == e.TenantId && db.Set<Offer>().Any(o => o.Id == x.OfferId && !o.Archived && o.Revision == x.Number));
            if (v == null) Fail(409, "OFFER_CHANGED", "La oferta cambió o ya no está disponible. Recarga el catálogo.");
            selected.Add((v!, line.Quantity)); total = checked(total + v!.PriceCents * line.Quantity);
            void Need(EventInventory i, int q) { if (q > 0) needs[i.Id] = needs.GetValueOrDefault(i.Id) + q; }
            Need(inventory.Single(x => x.ResourceId == null), v.Admissions * line.Quantity);
            if (v.ResourceId != null) Need(inventory.Single(x => x.ResourceId == v.ResourceId), v.ResourceUnits * line.Quantity);
        }
        if (selected.Sum(x => x.v.Admissions * x.q) > 1000) Fail(422, "TOO_MANY_ADMISSIONS", "Máximo 1000 accesos por pedido.");
        foreach (var (id, quantity) in needs) { var i = inventory.Single(x => x.Id == id); if (i.Blocked || i.Held + i.Sold + quantity > i.Limit) Fail(409, "RESOURCE_UNAVAILABLE", "Recurso o aforo sin disponibilidad."); }
        var hold = Scope(new Hold { UserId = buyer, CreatedAt = Now, ExpiresAt = Now.AddMinutes(Math.Clamp(holdMinutes ?? config.GetValue<int?>("Holds:Minutes") ?? 10, 1, 60)), TotalCents = total }, e); db.Add(hold);
        foreach (var (v, q) in selected) db.Add(Scope(new HoldLine { HoldId = hold.Id, OfferVersionId = v.Id, Quantity = q }, e));
        foreach (var (id, quantity) in needs) { inventory.Single(x => x.Id == id).Held += quantity; db.Add(Scope(new InventoryAllocation { HoldId = hold.Id, InventoryId = id, Quantity = quantity }, e)); }
        await db.SaveChangesAsync(); return hold;
    }
    public async Task<HoldDto> HoldDetail(Guid id, string actor)
    {
        var h = await db.Set<Hold>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == actor); if (h == null) Fail(404, "HOLD_NOT_FOUND", "Reserva no encontrada.");
        return await Transaction(h!.EventId, async e => { var current = await db.Set<Hold>().SingleAsync(x => x.Id == id); return new HoldDto(current.Id, current.EventId, current.Status, current.TotalCents, current.ExpiresAt, Now, await db.Set<HoldLine>().Where(x => x.HoldId == id).Select(x => new OfferLineRequest(x.OfferVersionId, x.Quantity)).ToListAsync()); });
    }
    public async Task<CommerceResult> Cancel(ClubEvent e, Guid id, string actor)
    {
        var h = await db.Set<Hold>().SingleOrDefaultAsync(x => x.Id == id && x.EventId == e.Id && x.UserId == actor); if (h == null) Fail(404, "HOLD_NOT_FOUND", "Reserva no encontrada.");
        if (h!.Status == "Converted") Fail(409, "HOLD_CONVERTED", "La reserva ya se confirmó.");
        if (h.Status == "Active") { await Release(h, "Cancelled"); var o = await db.Set<Order>().SingleOrDefaultAsync(x => x.HoldId == h.Id); if (o != null) o.Status = "Cancelled"; }
        return new(h.Id, h.Status, h.TotalCents, h.ExpiresAt);
    }
    public async Task<Order> CreateOrder(ClubEvent e, Guid holdId, string buyer, string source = "Online", string reason = "")
    {
        var h = await db.Set<Hold>().SingleOrDefaultAsync(x => x.Id == holdId && x.EventId == e.Id && x.UserId == buyer);
        if (h == null) Fail(404, "HOLD_NOT_FOUND", "Reserva no encontrada.");
        var existing = await db.Set<Order>().SingleOrDefaultAsync(x => x.HoldId == holdId); if (existing != null) return existing;
        if (h!.Status != "Active" || h.ExpiresAt <= Now) Fail(409, "HOLD_EXPIRED", "La reserva ya no está activa.");
        var order = Scope(new Order { HoldId = h.Id, UserId = buyer, Source = source, Reason = reason, TotalCents = source == "Complimentary" ? 0 : h.TotalCents, CreatedAt = Now }, e); db.Add(order);
        foreach (var l in await db.Set<HoldLine>().Where(x => x.HoldId == h.Id).ToListAsync()) {
            var v = await db.Set<OfferVersion>().SingleAsync(x => x.Id == l.OfferVersionId); var zone = v.ResourceId == null ? "Sin acceso" : (await db.EventResources.SingleAsync(x => x.Id == v.ResourceId)).Name;
            db.Add(Scope(new OrderLine { OrderId = order.Id, OfferVersionId = v.Id, Name = v.Name, UnitPriceCents = source == "Complimentary" ? 0 : v.PriceCents, Quantity = l.Quantity, Admissions = v.Admissions, Zone = zone, DrinksJson = v.DrinksJson }, e));
        }
        await db.SaveChangesAsync(); return order;
    }
    public async Task<CommerceResult> Pay(ClubEvent e, Guid orderId, string actor, bool approved, IPaymentGateway gateway, bool staff = false, ManualPaymentRequest? manual = null)
    {
        var o = await db.Set<Order>().SingleOrDefaultAsync(x => x.Id == orderId && x.EventId == e.Id && (staff || x.UserId == actor)); if (o == null) Fail(404, "ORDER_NOT_FOUND", "Pedido no encontrado.");
        if (o!.Source == "WhatsAppManual" && manual == null) Fail(403, "MANUAL_VERIFICATION_REQUIRED", "Solo un promotor autorizado puede verificar este pago.");
        if (o.Status == "Confirmed" || o.Status == "PaymentReview") return new(o.Id, o.Status, o.TotalCents);
        if (o.Status == "Cancelled") Fail(409, "ORDER_CANCELLED", "Pedido cancelado.");
        var payment = Scope(new PaymentAttempt { OrderId = o.Id, Status = gateway.Resolve(approved), AmountCents = o.TotalCents, CreatedAt = Now, PaymentMethod = manual?.Method, ReceiptReference = manual?.ReceiptReference?.Trim().ToUpperInvariant(), VerifiedBy = manual == null ? null : actor, PaidAt = manual?.PaidAt?.ToUniversalTime() }, e); db.Add(payment); Audit(e, actor, (manual == null ? "DemoPayment" : "ManualPayment") + payment.Status, o.Id, manual?.Note ?? "Pago simulado");
        if (payment.Status == "Failed") return new(o.Id, o.Status, o.TotalCents);
        var h = await db.Set<Hold>().SingleAsync(x => x.Id == o.HoldId);
        if (h.Status != "Active" || h.ExpiresAt <= Now) { o.Status = "PaymentReview"; Audit(e, actor, "LatePaymentReview", o.Id, "Pago posterior a liberación de inventario; sin beneficios"); return new(o.Id, o.Status, o.TotalCents); }
        foreach (var a in await db.Set<InventoryAllocation>().Where(x => x.HoldId == h.Id && x.Status == "Held").ToListAsync()) { var i = await db.Set<EventInventory>().SingleAsync(x => x.Id == a.InventoryId); i.Held -= a.Quantity; i.Sold += a.Quantity; a.Status = "Sold"; }
        h.Status = "Converted"; o.Status = "Confirmed";
        var drinks = new Dictionary<Guid, (string name, int q)>();
        foreach (var l in await db.Set<OrderLine>().Where(x => x.OrderId == o.Id).ToListAsync()) {
            for (var ordinal = 1; ordinal <= l.Admissions * l.Quantity; ordinal++) { var token = Token("t"); db.Add(Scope(new Ticket { OrderId = o.Id, OrderLineId = l.Id, Ordinal = ordinal, Zone = l.Zone, TokenHash = Hash(token), EncryptedToken = protector.Protect(token) }, e)); }
            foreach (var d in JsonSerializer.Deserialize<List<DrinkComponent>>(l.DrinksJson, Json)!) { var prev = drinks.GetValueOrDefault(d.ProductId); drinks[d.ProductId] = (d.Name, prev.q + d.Quantity * l.Quantity); }
        }
        if (drinks.Count > 0) { var token = Token("v"); var voucher = Scope(new ConsumptionVoucher { OrderId = o.Id, TokenHash = Hash(token), EncryptedToken = protector.Protect(token) }, e); db.Add(voucher); foreach (var (product, d) in drinks) db.Add(Scope(new ConsumptionEntitlement { VoucherId = voucher.Id, ProductId = product, Name = d.name, Granted = d.q }, e)); }
        Audit(e, actor, "BenefitsIssued", o.Id, o.Reason); return new(o.Id, o.Status, o.TotalCents);
    }
    private static string Token(string prefix) => prefix + "_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public void Audit(ClubEvent e, string actor, string action, Guid subject, string reason) => db.Add(Scope(new AuditEntry { ActorId = actor, Action = action, SubjectId = subject, Reason = reason, CreatedAt = Now, CorrelationId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N") }, e));
    public async Task<OrderDto> OrderDetail(Guid id, string actor)
    {
        var o = await db.Set<Order>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == actor); if (o == null) Fail(404, "ORDER_NOT_FOUND", "Pedido no encontrado.");
        return await Transaction(o!.EventId, async e => {
            var current = await db.Set<Order>().SingleAsync(x => x.Id == id);
            var lines = await db.Set<OrderLine>().Where(x => x.OrderId == id).OrderBy(x => x.Id).ToListAsync();
            var tickets = await db.Set<Ticket>().Where(x => x.OrderId == id).OrderBy(x => x.OrderLineId).ThenBy(x => x.Ordinal).ToListAsync();
            var voucher = await db.Set<ConsumptionVoucher>().SingleOrDefaultAsync(x => x.OrderId == id);
            VoucherDto? v = voucher == null ? null : new(voucher.Id, protector.Unprotect(voucher.EncryptedToken), await db.Set<ConsumptionEntitlement>().Where(x => x.VoucherId == voucher.Id).OrderBy(x => x.Name).Select(x => new EntitlementDto(x.Name, x.Granted, x.Delivered)).ToListAsync());
            return new OrderDto(current.Id, e.Id, e.Name, current.Status, current.Source, current.TotalCents, "PEN", current.CreatedAt, lines.Select(x => new OrderLineDto(x.Name, x.Quantity, x.UnitPriceCents, x.Admissions, x.Zone, JsonSerializer.Deserialize<List<DrinkComponent>>(x.DrinksJson, Json)!)).ToList(), tickets.Select(x => new TicketDto(x.Id, x.Ordinal, x.Zone, x.Status, protector.Unprotect(x.EncryptedToken))).ToList(), v, await db.Set<PaymentAttempt>().Where(x => x.OrderId == id).OrderBy(x => x.CreatedAt).Select(x => x.Status).ToListAsync(), current.Source == "WhatsAppManual" ? await ManualInfo(e, current) : null);
        });
    }
}
