using System.Security.Claims;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Api;
public static class CommerceEndpoints
{
    private static string Actor(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string Key(HttpContext c) => c.Request.Headers["Idempotency-Key"].ToString();
    private static async Task<IResult> Run<T>(Func<Task<T>> action) { try { return Results.Ok(await action()); } catch (CommerceException ex) { return Results.Problem(statusCode: ex.Status, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code, ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() }); } }
    private static async Task<Guid> HoldEvent(AppDbContext db, Guid id, string actor) => await db.Set<Hold>().Where(x => x.Id == id && x.UserId == actor).Select(x => (Guid?)x.EventId).SingleOrDefaultAsync() ?? throw new CommerceException(404, "HOLD_NOT_FOUND", "Reserva no encontrada.");
    private static async Task<Guid> OrderEvent(AppDbContext db, Guid id, string actor) => await db.Set<Order>().Where(x => x.Id == id && x.UserId == actor).Select(x => (Guid?)x.EventId).SingleOrDefaultAsync() ?? throw new CommerceException(404, "ORDER_NOT_FOUND", "Pedido no encontrado.");
    public static void MapCommerce(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/public/events/{eventId:guid}/catalog", (Guid eventId, CommerceService s) => Run(() => s.Transaction(eventId, e => s.Catalog(e)))).Produces<CatalogDto>().RequireRateLimiting("commerce");
        var buyer = api.MapGroup("").RequireAuthorization().RequireRateLimiting("commerce");
        buyer.MapPost("/holds", (HoldRequest r, HttpContext c, CommerceService s) => Run(() => s.Transaction(r.EventId, async e => { var h = await s.CreateHold(e, Actor(c), r.Lines); return new CommerceResult(h.Id, h.Status, h.TotalCents, h.ExpiresAt); }, Actor(c), "hold", Key(c), r))).Produces<CommerceResult>();
        buyer.MapGet("/me/holds/{id:guid}", (Guid id, HttpContext c, CommerceService s) => Run(() => s.HoldDetail(id, Actor(c)))).Produces<HoldDto>();
        buyer.MapPost("/me/holds/{id:guid}/cancel", (Guid id, HttpContext c, CommerceService s, AppDbContext db) => Run(async () => await s.Transaction(await HoldEvent(db, id, Actor(c)), e => s.Cancel(e, id, Actor(c)), Actor(c), "cancel", Key(c), new { id }))).Produces<CommerceResult>();
        buyer.MapPost("/orders", (OrderRequest r, HttpContext c, CommerceService s, AppDbContext db) => Run(async () => await s.Transaction(await HoldEvent(db, r.HoldId, Actor(c)), async e => { var o = await s.CreateOrder(e, r.HoldId, Actor(c)); return new CommerceResult(o.Id, o.Status, o.TotalCents); }, Actor(c), "order", Key(c), r))).Produces<CommerceResult>();
        buyer.MapGet("/me/orders", async (HttpContext c, AppDbContext db) => await db.Set<Order>().Where(x => x.UserId == Actor(c)).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new CommerceResult(x.Id, x.Status, x.TotalCents)).ToListAsync()).Produces<List<CommerceResult>>();
        buyer.MapGet("/me/orders/{id:guid}", (Guid id, HttpContext c, CommerceService s) => Run(() => s.OrderDetail(id, Actor(c)))).Produces<OrderDto>();
        buyer.MapPost("/demo/orders/{id:guid}/payment-result", (Guid id, DemoPaymentRequest r, HttpContext c, CommerceService s, AppDbContext db, IConfiguration config, IWebHostEnvironment env) => Run(async () => {
            RequireDemo(config, env); return await s.Transaction(await OrderEvent(db, id, Actor(c)), e => s.Pay(e, id, Actor(c), r.Approved, new DemoPaymentGateway()), Actor(c), "payment", Key(c), new { id, r.Approved });
        })).Produces<CommerceResult>();
        var admin = api.MapGroup("/tenants/{tenantId:guid}/venues/{venueId:guid}").RequireAuthorization("VenueAdmin");
        admin.MapGet("/products", async (Guid tenantId, AppDbContext db) => await db.Set<Product>().Where(x => x.TenantId == tenantId && !x.Archived).OrderBy(x => x.Name).ToListAsync()).Produces<List<Product>>();
        admin.MapPost("/products", (Guid tenantId, ProductSave r, AppDbContext db) => Run(async () => { if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 100) throw new CommerceException(422, "INVALID_NAME", "Nombre de 1 a 100 caracteres requerido."); var p = new Product { TenantId = tenantId, Name = r.Name.Trim() }; db.Add(p); await db.SaveChangesAsync(); return p; })).Produces<Product>();
        admin.MapPost("/events/{eventId:guid}/offers", (Guid tenantId, Guid venueId, Guid eventId, OfferSave r, HttpContext c, CommerceService s) => Run(() => s.Transaction(eventId, e => { CheckScope(e, tenantId, venueId); return s.SaveOffer(e, r, Actor(c)); }))).Produces<OfferDto>();
        admin.MapPost("/events/{eventId:guid}/offers/{offerId:guid}/archive", (Guid tenantId, Guid venueId, Guid eventId, Guid offerId, HttpContext c, CommerceService s, AppDbContext db) => Run(() => s.Transaction(eventId, async e => { CheckScope(e, tenantId, venueId); var o = await db.Set<Offer>().SingleOrDefaultAsync(x => x.Id == offerId && x.EventId == e.Id); if (o == null) throw new CommerceException(404, "OFFER_NOT_FOUND", "Oferta no encontrada."); o.Archived = true; s.Audit(e, Actor(c), "OfferArchived", o.Id, ""); return new { o.Id }; })));
        admin.MapPost("/events/{eventId:guid}/inventory/{inventoryId:guid}/block", (Guid tenantId, Guid venueId, Guid eventId, Guid inventoryId, BlockRequest r, HttpContext c, CommerceService s, AppDbContext db) => Run(() => s.Transaction(eventId, async e => { CheckScope(e, tenantId, venueId); var i = await db.Set<EventInventory>().SingleOrDefaultAsync(x => x.Id == inventoryId && x.EventId == e.Id); if (i == null) throw new CommerceException(404, "INVENTORY_NOT_FOUND", "Inventario no encontrado."); i.Blocked = r.Blocked; s.Audit(e, Actor(c), "InventoryBlocked:" + r.Blocked, i.Id, ""); return new { i.Id, i.Blocked }; })));
        var sales = api.MapGroup("/tenants/{tenantId:guid}/venues/{venueId:guid}/events/{eventId:guid}").RequireAuthorization("Sales").RequireRateLimiting("commerce");
        sales.MapPost("/cash-orders", (Guid tenantId, Guid venueId, Guid eventId, StaffSaleRequest r, HttpContext c, CommerceService s, AppDbContext db, IConfiguration config, IWebHostEnvironment env) => StaffSale(tenantId, venueId, eventId, r, c, s, db, config, env, false)).Produces<CommerceResult>();
        sales.MapPost("/complimentary-orders", (Guid tenantId, Guid venueId, Guid eventId, StaffSaleRequest r, HttpContext c, CommerceService s, AppDbContext db, IConfiguration config, IWebHostEnvironment env) => StaffSale(tenantId, venueId, eventId, r, c, s, db, config, env, true)).RequireAuthorization("VenueAdmin").Produces<CommerceResult>();
        sales.MapGet("/customers", (Guid tenantId, Guid venueId, Guid eventId, AppDbContext db, IConfiguration config, IWebHostEnvironment env) => Run(async () => {
            if (!(config["Payments:Provider"] == "WhatsAppManual" && (env.IsDevelopment() || env.IsEnvironment("Demo")))) RequireDemo(config, env);
            if (!await db.Events.AnyAsync(x => x.Id == eventId && x.TenantId == tenantId && x.VenueId == venueId && x.Status == "Published")) throw new CommerceException(404, "EVENT_NOT_FOUND", "Evento no encontrado.");
            return await db.Users.Where(x => !db.Memberships.Any(m => m.UserId == x.Id && m.Active)).OrderBy(x => x.Email).Take(50).Select(x => x.Email!).ToListAsync();
        })).Produces<List<string>>();
    }
    private static void CheckScope(ClubEvent e, Guid t, Guid v) { if (e.TenantId != t || e.VenueId != v) throw new CommerceException(404, "EVENT_NOT_FOUND", "Evento no encontrado."); }
    private static void RequireDemo(IConfiguration config, IWebHostEnvironment env) { if (!(env.IsDevelopment() || env.IsEnvironment("Demo")) || config["Payments:Provider"] != "Demo") throw new CommerceException(403, "DEMO_DISABLED", "Los pagos simulados están deshabilitados."); }
    private static Task<IResult> StaffSale(Guid t, Guid v, Guid eventId, StaffSaleRequest r, HttpContext c, CommerceService s, AppDbContext db, IConfiguration config, IWebHostEnvironment env, bool complimentary) => Run(async () => {
        if (!complimentary) RequireDemo(config, env);
        return await s.Transaction(eventId, async e => {
            CheckScope(e, t, v);
            if (string.IsNullOrWhiteSpace(r.CustomerEmail) || r.CustomerEmail.Length > 254) throw new CommerceException(422, "CUSTOMER_REQUIRED", "Selecciona un cliente existente.");
            if (complimentary && (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 500)) throw new CommerceException(422, "REASON_REQUIRED", "La cortesía requiere motivo de hasta 500 caracteres.");
            var customer = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == r.CustomerEmail.ToUpper()); if (customer == null) throw new CommerceException(404, "CUSTOMER_NOT_FOUND", "Selecciona un cliente existente.");
            var hold = await s.CreateHold(e, customer.Id, r.Lines); var order = await s.CreateOrder(e, hold.Id, customer.Id, complimentary ? "Complimentary" : "CashDemo", complimentary ? r.Reason.Trim() : "Venta presencial simulada");
            s.Audit(e, Actor(c), complimentary ? "ComplimentaryCreated" : "CashDemoCreated", order.Id, order.Reason);
            return await s.Pay(e, order.Id, Actor(c), complimentary || r.Approved, new DemoPaymentGateway(), true);
        }, Actor(c), complimentary ? "complimentary" : "cash", Key(c), new { eventId, request = r });
    });
}
public record BlockRequest(bool Blocked);
public class HoldExpiryWorker(IServiceScopeFactory scopes, ILogger<HoldExpiryWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            try { using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var ids = await db.Set<Hold>().Where(x => x.Status == "Active" && x.ExpiresAt <= DateTimeOffset.UtcNow).Select(x => x.EventId).Distinct().Take(100).ToListAsync(stoppingToken);
                foreach (var id in ids) { using var work = scopes.CreateScope(); await work.ServiceProvider.GetRequiredService<CommerceService>().Transaction(id, e => Task.FromResult(true)); }
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; } catch (Exception ex) { log.LogError(ex, "Hold expiration failed; retrying next interval"); }
        }
    }
}




