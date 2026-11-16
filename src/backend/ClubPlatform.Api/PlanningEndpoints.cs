using System.Security.Claims;
using System.Text.Json;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Api;

public record VenueCreate(string Name, int Capacity);
public record LayoutCreate(string Name);
public record LayoutSave(string Name, int ExpectedRevision, List<MapElement> Elements);
public record RevisionRequest(int ExpectedRevision);
public record VersionResponse(Guid Id, int Number, string Name, int SourceRevision, DateTimeOffset PublishedAt, List<MapElement> Elements);
public record LayoutResponse(Guid Id, string Name, int Revision, List<MapElement> Elements, List<VersionResponse> Versions);
public record LayoutSummary(Guid Id, string Name, int Revision);
public record EventSave(string Name, int Capacity, DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset EntryOpensAt, DateTimeOffset EntryClosesAt, DateTimeOffset RedemptionOpensAt, DateTimeOffset RedemptionClosesAt, Guid? LayoutVersionId, int ExpectedRevision = 0);
public record EventResponse(Guid Id, Guid TenantId, Guid VenueId, string Name, string Status, int Revision, int Capacity, DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset EntryOpensAt, DateTimeOffset EntryClosesAt, DateTimeOffset RedemptionOpensAt, DateTimeOffset RedemptionClosesAt, Guid? LayoutVersionId, string VenueName, string TimeZone, List<MapElement> Elements, List<ResourceResponse> Resources);
public record ResourceResponse(Guid Id, Guid SourceElementId, string Name, string Mode, int HumanCapacity, int AllocationLimit);

public static class PlanningEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static List<MapElement> Elements(string json) => JsonSerializer.Deserialize<List<MapElement>>(json, Json)!;
    private static IResult Invalid(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["configuration"] = [message] });
    private static IResult Conflict(string message) => Results.Problem(statusCode: 409, title: message);
    private static VenueResponse VenueDto(Venue v) => new(v.Id, v.TenantId, v.Name, v.TimeZone, v.Capacity);
    // All writes lock the venue first. This also serializes capacity changes, publication and archive.
    private static async Task<IResult> Write(AppDbContext db, Guid tenantId, Guid venueId, Func<Venue, Task<IResult>> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var venue = await db.Venues.FromSqlInterpolated($"SELECT * FROM app.\"Venues\" WHERE \"TenantId\" = {tenantId} AND \"Id\" = {venueId} FOR UPDATE").SingleOrDefaultAsync();
        if (venue == null || venue.Archived) return Results.NotFound();
        var result = await action(venue);
        await tx.CommitAsync();
        return result;
    }
    private static IQueryable<LayoutTemplate> Templates(AppDbContext db, Guid t, Guid v) => db.LayoutTemplates.Where(x => x.TenantId == t && x.VenueId == v);
    private static IQueryable<ClubEvent> Events(AppDbContext db, Guid t, Guid v) => db.Events.Where(x => x.TenantId == t && x.VenueId == v);
    private static IQueryable<LayoutVersion> Versions(AppDbContext db, Guid t, Guid v) => db.LayoutVersions.Where(x => x.TenantId == t && x.VenueId == v);
    private static async Task<List<MapElement>> DraftElements(AppDbContext db, Guid t, Guid v, Guid template) => (await db.LayoutElements.Where(x => x.TenantId == t && x.VenueId == v && x.TemplateId == template).OrderBy(x => x.Id).ToListAsync()).Select(x => new MapElement(x.Id, x.Name, x.Kind, x.Mode, x.Capacity, JsonSerializer.Deserialize<Geometry>(x.GeometryJson, Json)!)).ToList();
    private static VersionResponse VersionDto(LayoutVersion v) => new(v.Id, v.Number, v.Name, v.SourceRevision, v.PublishedAt, Elements(v.ElementsJson));
    private static async Task<EventResponse> EventDto(AppDbContext db, ClubEvent e) => new(e.Id, e.TenantId, e.VenueId, e.Name, e.Status, e.Revision, e.Capacity, e.StartsAt, e.EndsAt, e.EntryOpensAt, e.EntryClosesAt, e.RedemptionOpensAt, e.RedemptionClosesAt, e.LayoutVersionId, e.VenueName, e.TimeZone, Elements(e.MapJson), await db.EventResources.Where(x => x.TenantId == e.TenantId && x.VenueId == e.VenueId && x.EventId == e.Id).OrderBy(x => x.Name).Select(x => new ResourceResponse(x.Id, x.SourceElementId, x.Name, x.Mode, x.HumanCapacity, x.AllocationLimit)).ToListAsync());
    private static void Apply(ClubEvent e, EventSave r)
    {
        e.Name = r.Name?.Trim() ?? ""; e.Capacity = r.Capacity; e.LayoutVersionId = r.LayoutVersionId;
        e.StartsAt = r.StartsAt.ToUniversalTime(); e.EndsAt = r.EndsAt.ToUniversalTime();
        e.EntryOpensAt = r.EntryOpensAt.ToUniversalTime(); e.EntryClosesAt = r.EntryClosesAt.ToUniversalTime();
        e.RedemptionOpensAt = r.RedemptionOpensAt.ToUniversalTime(); e.RedemptionClosesAt = r.RedemptionClosesAt.ToUniversalTime();
    }
    public static void MapPlanning(this WebApplication app)
    {
        var tenants = app.MapGroup("/api/v1/tenants/{tenantId:guid}");
        tenants.MapGet("/venues", async (Guid tenantId, HttpContext ctx, AppDbContext db) =>
        {
            var uid = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var admin = await db.Memberships.AnyAsync(x => x.TenantId == tenantId && x.UserId == uid && x.Active && x.Role == StaffRoles.TenantAdmin);
            return Results.Ok(await db.Venues.Where(x => x.TenantId == tenantId && !x.Archived && (admin || db.Assignments.Any(a => a.TenantId == tenantId && a.VenueId == x.Id && a.UserId == uid))).OrderBy(x => x.Name).Select(x => new VenueResponse(x.Id, x.TenantId, x.Name, x.TimeZone, x.Capacity)).ToListAsync());
        }).RequireAuthorization("Staff").Produces<List<VenueResponse>>();
        tenants.MapPost("/venues", async (Guid tenantId, VenueCreate r, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 160 || r.Capacity < 1 || r.Capacity > 100000) return Invalid("Nombre de hasta 160 caracteres y aforo de 1 a 100000 requeridos.");
            var v = new Venue { Id = Guid.NewGuid(), TenantId = tenantId, Name = r.Name.Trim(), Capacity = r.Capacity }; db.Venues.Add(v); await db.SaveChangesAsync(); return Results.Ok(VenueDto(v));
        }).RequireAuthorization("TenantAdmin").Produces<VenueResponse>();
        var scope = app.MapGroup("/api/v1/tenants/{tenantId:guid}/venues/{venueId:guid}");
        scope.MapPut("", (Guid tenantId, Guid venueId, VenueUpdate r, AppDbContext db) => Write(db, tenantId, venueId, async v =>
        {
            if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 160 || r.Capacity is < 1 or > 100000) return Invalid("Nombre o aforo inválido.");
            if (r.Capacity.HasValue && await Events(db, tenantId, venueId).AnyAsync(x => x.Capacity > r.Capacity.Value)) return Conflict("El aforo no puede ser menor al de un evento existente.");
            v.Name = r.Name.Trim(); if (r.Capacity.HasValue) v.Capacity = r.Capacity.Value; await db.SaveChangesAsync(); return Results.NoContent();
        })).RequireAuthorization("VenueAdmin");
        scope.MapDelete("", (Guid tenantId, Guid venueId, AppDbContext db) => Write(db, tenantId, venueId, async v =>
        {
            if (await Events(db, tenantId, venueId).AnyAsync(x => x.Status == "Published")) return Conflict("Una sede con eventos publicados no se puede archivar.");
            v.Archived = true; await db.Sessions.Where(x => x.TenantId == tenantId && x.VenueId == venueId).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, (Guid?)null).SetProperty(x => x.VenueId, (Guid?)null)); await db.SaveChangesAsync(); return Results.NoContent();
        })).RequireAuthorization("TenantAdmin");
        scope.MapGet("/layouts", async (Guid tenantId, Guid venueId, AppDbContext db) => Results.Ok(await Templates(db, tenantId, venueId).OrderBy(x => x.Name).Select(x => new LayoutSummary(x.Id, x.Name, x.Revision)).ToListAsync())).RequireAuthorization("VenueAdmin").Produces<List<LayoutSummary>>();
        scope.MapPost("/layouts", (Guid tenantId, Guid venueId, LayoutCreate r, AppDbContext db) => Write(db, tenantId, venueId, async _ =>
        {
            if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 160) return Invalid("Nombre de plantilla requerido (máximo 160 caracteres).");
            var template = new LayoutTemplate { Id = Guid.NewGuid(), TenantId = tenantId, VenueId = venueId, Name = r.Name.Trim() }; db.LayoutTemplates.Add(template); await db.SaveChangesAsync(); return Results.Ok(new LayoutSummary(template.Id, template.Name, template.Revision));
        })).RequireAuthorization("VenueAdmin").Produces<LayoutSummary>();
        scope.MapGet("/layouts/{templateId:guid}", async (Guid tenantId, Guid venueId, Guid templateId, AppDbContext db) =>
        {
            var template = await Templates(db, tenantId, venueId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == templateId); if (template == null) return Results.NotFound();
            return Results.Ok(new LayoutResponse(template.Id, template.Name, template.Revision, await DraftElements(db, tenantId, venueId, templateId), (await Versions(db, tenantId, venueId).Where(x => x.TemplateId == templateId).OrderByDescending(x => x.Number).ToListAsync()).Select(VersionDto).ToList()));
        }).RequireAuthorization("VenueAdmin").Produces<LayoutResponse>();
        scope.MapPut("/layouts/{templateId:guid}", (Guid tenantId, Guid venueId, Guid templateId, LayoutSave r, AppDbContext db) => Write(db, tenantId, venueId, async venue =>
        {
            var template = await Templates(db, tenantId, venueId).SingleOrDefaultAsync(x => x.Id == templateId); if (template == null) return Results.NotFound();
            if (template.Revision != r.ExpectedRevision) return Conflict("El mapa cambió en otra sesión. Reabre el borrador antes de guardar.");
            if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 160) return Invalid("Nombre de plantilla inválido.");
            var error = PlanningRules.ValidateMap(r.Elements, venue.Capacity, false); if (error != null) return Invalid(error);
            await db.LayoutElements.Where(x => x.TenantId == tenantId && x.VenueId == venueId && x.TemplateId == templateId).ExecuteDeleteAsync();
            foreach (var e in r.Elements) db.LayoutElements.Add(new() { Id = e.Id, TenantId = tenantId, VenueId = venueId, TemplateId = templateId, Name = e.Name.Trim(), Kind = e.Kind, Mode = e.Mode, Capacity = e.Capacity, GeometryJson = JsonSerializer.Serialize(e.Geometry, Json) });
            template.Name = r.Name.Trim(); template.Revision++; await db.SaveChangesAsync(); return Results.Ok(new LayoutSummary(template.Id, template.Name, template.Revision));
        })).RequireAuthorization("VenueAdmin").Produces<LayoutSummary>();
        scope.MapPost("/layouts/{templateId:guid}/publish", (Guid tenantId, Guid venueId, Guid templateId, RevisionRequest r, AppDbContext db) => Write(db, tenantId, venueId, async venue =>
        {
            var template = await Templates(db, tenantId, venueId).SingleOrDefaultAsync(x => x.Id == templateId); if (template == null) return Results.NotFound(); if (template.Revision != r.ExpectedRevision) return Conflict("El borrador cambió. Reabre antes de publicar.");
            var previous = await Versions(db, tenantId, venueId).SingleOrDefaultAsync(x => x.TemplateId == templateId && x.SourceRevision == template.Revision); if (previous != null) return Results.Ok(VersionDto(previous));
            var elements = await DraftElements(db, tenantId, venueId, templateId); var error = PlanningRules.ValidateMap(elements, venue.Capacity, true); if (error != null) return Invalid(error);
            var version = new LayoutVersion { Id = Guid.NewGuid(), TenantId = tenantId, VenueId = venueId, TemplateId = templateId, Name = template.Name, SourceRevision = template.Revision, Number = (await Versions(db, tenantId, venueId).Where(x => x.TemplateId == templateId).MaxAsync(x => (int?)x.Number) ?? 0) + 1, ElementsJson = JsonSerializer.Serialize(elements, Json), PublishedAt = DateTimeOffset.UtcNow };
            db.LayoutVersions.Add(version); await db.SaveChangesAsync(); return Results.Ok(VersionDto(version));
        })).RequireAuthorization("VenueAdmin").Produces<VersionResponse>();
        scope.MapGet("/versions", async (Guid tenantId, Guid venueId, AppDbContext db) => Results.Ok((await Versions(db, tenantId, venueId).OrderByDescending(x => x.PublishedAt).ToListAsync()).Select(VersionDto).ToList())).RequireAuthorization("VenueAdmin").Produces<List<VersionResponse>>();
        scope.MapGet("/events", async (Guid tenantId, Guid venueId, AppDbContext db) =>
        {
            var result = new List<EventResponse>(); foreach (var e in await Events(db, tenantId, venueId).OrderBy(x => x.StartsAt).ToListAsync()) result.Add(await EventDto(db, e)); return Results.Ok(result);
        }).RequireAuthorization("Staff").Produces<List<EventResponse>>();
        scope.MapGet("/events/{eventId:guid}", async (Guid tenantId, Guid venueId, Guid eventId, AppDbContext db) =>
        {
            var e = await Events(db, tenantId, venueId).SingleOrDefaultAsync(x => x.Id == eventId); return e == null ? Results.NotFound() : Results.Ok(await EventDto(db, e));
        }).RequireAuthorization("Staff").Produces<EventResponse>();
        scope.MapPost("/events", (Guid tenantId, Guid venueId, EventSave r, AppDbContext db) => Write(db, tenantId, venueId, async venue =>
        {
            var e = new ClubEvent { Id = Guid.NewGuid(), TenantId = tenantId, VenueId = venueId, VenueName = venue.Name }; Apply(e, r); var error = PlanningRules.ValidateEvent(e, venue.Capacity); if (error != null) return Invalid(error);
            if (e.LayoutVersionId != null && !await Versions(db, tenantId, venueId).AnyAsync(x => x.Id == e.LayoutVersionId)) return Results.NotFound();
            db.Events.Add(e); await db.SaveChangesAsync(); return Results.Ok(await EventDto(db, e));
        })).RequireAuthorization("VenueAdmin").Produces<EventResponse>();
        scope.MapPut("/events/{eventId:guid}", (Guid tenantId, Guid venueId, Guid eventId, EventSave r, AppDbContext db) => Write(db, tenantId, venueId, async venue =>
        {
            var e = await Events(db, tenantId, venueId).SingleOrDefaultAsync(x => x.Id == eventId); if (e == null) return Results.NotFound(); if (e.Status != "Draft") return Conflict("Solo los borradores pueden editarse."); if (e.Revision != r.ExpectedRevision) return Conflict("El evento cambió. Recarga antes de guardar.");
            Apply(e, r); var error = PlanningRules.ValidateEvent(e, venue.Capacity); if (error != null) return Invalid(error); if (e.LayoutVersionId != null && !await Versions(db, tenantId, venueId).AnyAsync(x => x.Id == e.LayoutVersionId)) return Results.NotFound();
            e.Revision++; await db.SaveChangesAsync(); return Results.Ok(await EventDto(db, e));
        })).RequireAuthorization("VenueAdmin").Produces<EventResponse>();
        scope.MapDelete("/events/{eventId:guid}", (Guid tenantId, Guid venueId, Guid eventId, AppDbContext db) => Write(db, tenantId, venueId, async _ =>
        {
            var e = await Events(db, tenantId, venueId).SingleOrDefaultAsync(x => x.Id == eventId); if (e == null) return Results.NotFound(); if (e.Status != "Draft") return Conflict("Un evento publicado no se puede eliminar."); db.Events.Remove(e); await db.SaveChangesAsync(); return Results.NoContent();
        })).RequireAuthorization("VenueAdmin");
        scope.MapPost("/events/{eventId:guid}/publish", (Guid tenantId, Guid venueId, Guid eventId, RevisionRequest r, AppDbContext db) => Write(db, tenantId, venueId, async venue =>
        {
            var e = await Events(db, tenantId, venueId).SingleOrDefaultAsync(x => x.Id == eventId); if (e == null) return Results.NotFound(); if (e.Status == "Published") return Results.Ok(await EventDto(db, e)); if (e.Revision != r.ExpectedRevision) return Conflict("El evento cambió. Recarga antes de publicar.");
            var error = PlanningRules.ValidateEvent(e, venue.Capacity); if (error != null) return Invalid(error);
            var version = await Versions(db, tenantId, venueId).SingleOrDefaultAsync(x => x.Id == e.LayoutVersionId); if (version == null) return Invalid("Selecciona una versión publicada de mapa de esta sede.");
            var elements = Elements(version.ElementsJson); error = PlanningRules.ValidateMap(elements, venue.Capacity, true); if (error != null) return Invalid(error);
            if (e.Capacity > elements.Where(x => x.Mode != "Decoration").Sum(x => x.Capacity)) return Invalid("El aforo del evento supera la capacidad del mapa publicado.");
            e.MapJson = version.ElementsJson; e.Status = "Published"; e.VenueName = venue.Name; e.TimeZone = venue.TimeZone; e.Revision++;
            foreach (var item in elements.Where(x => x.Mode != "Decoration")) db.EventResources.Add(new() { Id = Guid.NewGuid(), TenantId = tenantId, VenueId = venueId, EventId = e.Id, SourceElementId = item.Id, Name = item.Name, Mode = item.Mode, HumanCapacity = item.Capacity, AllocationLimit = item.Mode == "Exclusive" ? 1 : item.Capacity });
            await db.SaveChangesAsync(); return Results.Ok(await EventDto(db, e));
        })).RequireAuthorization("VenueAdmin").Produces<EventResponse>();
        app.MapGet("/api/v1/public/events", async (AppDbContext db) =>
        {
            var result = new List<EventResponse>(); foreach (var e in await db.Events.Where(x => x.Status == "Published" && db.Venues.Any(v => v.TenantId == x.TenantId && v.Id == x.VenueId && !v.Archived)).OrderBy(x => x.StartsAt).Take(100).ToListAsync()) result.Add(await EventDto(db, e)); return Results.Ok(result);
        }).Produces<List<EventResponse>>();
        app.MapGet("/api/v1/public/events/{eventId:guid}", async (Guid eventId, AppDbContext db) =>
        {
            var e = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.Status == "Published" && db.Venues.Any(v => v.TenantId == x.TenantId && v.Id == x.VenueId && !v.Archived)); return e == null ? Results.NotFound() : Results.Ok(await EventDto(db, e));
        }).Produces<EventResponse>();
    }
}
