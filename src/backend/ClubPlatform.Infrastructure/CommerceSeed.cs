using System.Text.Json;
using ClubPlatform.Core;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Infrastructure;
public static class CommerceSeed
{
    public static async Task Run(AppDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73492612)");
        var eventId = Guid.Parse("aaaaaaaa-3333-0000-0000-000000000001");
        if (await db.Events.AnyAsync(x => x.Id == eventId)) { await tx.CommitAsync(); return; }
        var t = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"); var venue = await db.Venues.SingleAsync(x => x.Id == Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var template = new LayoutTemplate { Id = Guid.NewGuid(), TenantId = t, VenueId = venue.Id, Name = "Mapa demo de ventas" }; db.Add(template);
        List<MapElement> elements = [new(Guid.NewGuid(), "Box B3", "Box", "Exclusive", 10, new(.08,.1,.25,.25,0)), new(Guid.NewGuid(), "Box B4", "Box", "Exclusive", 10, new(.66,.1,.25,.25,0)), new(Guid.NewGuid(), "General", "General", "Capacity",80,new(.1,.48,.8,.4,0))];
        foreach (var el in elements) db.Add(new LayoutElement { Id = el.Id, TenantId = t, VenueId = venue.Id, TemplateId = template.Id, Name = el.Name, Kind = el.Kind, Mode = el.Mode, Capacity = el.Capacity, GeometryJson = JsonSerializer.Serialize(el.Geometry, json) });
        var version = new LayoutVersion { Id = Guid.NewGuid(), TenantId = t, VenueId = venue.Id, TemplateId = template.Id, Name = template.Name, Number = 1, SourceRevision = 1, ElementsJson = JsonSerializer.Serialize(elements,json), PublishedAt = DateTimeOffset.UtcNow }; db.Add(version);
        var start = DateTimeOffset.Parse("2027-01-16T03:00:00Z");
        var e = new ClubEvent { Id = eventId, TenantId = t, VenueId = venue.Id, Name = "Noche demo · Boxes y entradas", Status = "Published", Capacity = 100, StartsAt = start, EndsAt = start.AddHours(7), EntryOpensAt = start.AddMinutes(-30), EntryClosesAt = start.AddHours(5), RedemptionOpensAt = start, RedemptionClosesAt = start.AddHours(6.5), LayoutVersionId = version.Id, MapJson = version.ElementsJson, VenueName = venue.Name }; db.Add(e);
        var whisky = new Product { TenantId = t, Name = "Whisky" }; var soda = new Product { TenantId = t, Name = "Gaseosa" }; db.AddRange(whisky, soda);
        db.Add(new EventInventory { TenantId = t, VenueId = venue.Id, EventId = e.Id, Limit = 100 });
        foreach (var el in elements) {
            var resource = new EventResource { Id = Guid.NewGuid(), TenantId = t, VenueId = venue.Id, EventId = e.Id, SourceElementId = el.Id, Name = el.Name, Mode = el.Mode, HumanCapacity = el.Capacity, AllocationLimit = el.Mode == "Exclusive" ? 1 : el.Capacity }; db.Add(resource);
            db.Add(new EventInventory { TenantId = t, VenueId = venue.Id, EventId = e.Id, ResourceId = resource.Id, Limit = resource.AllocationLimit });
            var offer = new Offer { TenantId = t, VenueId = venue.Id, EventId = e.Id, Name = el.Mode == "Exclusive" ? el.Name + " + 10 accesos + whisky + 2 gaseosas" : "Entrada general", Revision = 1 }; db.Add(offer);
            db.Add(new OfferVersion { TenantId = t, VenueId = venue.Id, EventId = e.Id, OfferId = offer.Id, Number = 1, Name = offer.Name, PriceCents = el.Mode == "Exclusive" ? 100000 : 5000, ResourceId = resource.Id, ResourceUnits = 1, Admissions = el.Mode == "Exclusive" ? 10 : 1, DrinksJson = JsonSerializer.Serialize(el.Mode == "Exclusive" ? new List<DrinkComponent> { new(whisky.Id, whisky.Name, 1), new(soda.Id, soda.Name, 2) } : [], json), CreatedAt = DateTimeOffset.UtcNow });
        }
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
