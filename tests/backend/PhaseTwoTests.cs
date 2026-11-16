using System.Net;
using System.Net.Http.Json;
using ClubPlatform.Api;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClubPlatform.Tests;

public partial class PhaseOneTests
{
    private static List<MapElement> SampleMap() => [
        new(Guid.NewGuid(),"Box B3","Box","Exclusive",10,new(.1,.1,.2,.15,20)),
        new(Guid.NewGuid(),"Box B4","Box","Exclusive",10,new(.4,.1,.2,.15,0)),
        new(Guid.NewGuid(),"General","General","Capacity",80,new(.1,.4,.7,.3,0)),
        new(Guid.NewGuid(),"Barra","Bar","Decoration",0,new(.8,.1,.1,.3,0))
    ];
    private static EventSave Night(Guid? version, int revision = 0, int capacity = 100) => new("Noche de apertura", capacity, DateTimeOffset.Parse("2027-01-15T22:00:00-05:00"), DateTimeOffset.Parse("2027-01-16T05:00:00-05:00"), DateTimeOffset.Parse("2027-01-15T21:30:00-05:00"), DateTimeOffset.Parse("2027-01-16T03:00:00-05:00"), DateTimeOffset.Parse("2027-01-15T22:00:00-05:00"), DateTimeOffset.Parse("2027-01-16T04:30:00-05:00"), version, revision);
    private async Task<T> Success<T>(HttpResponseMessage response)
    { Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); return (await response.Content.ReadFromJsonAsync<T>())!; }
    private async Task<VersionResponse> PublishedMap(HttpClient c, string venue = A)
    {
        var template = await Success<LayoutSummary>(await Mutate(c, venue + "/layouts", HttpMethod.Post, new LayoutCreate("Mapa principal")));
        await Success<LayoutSummary>(await Mutate(c, venue + $"/layouts/{template.Id}", HttpMethod.Put, new LayoutSave(template.Name, template.Revision, SampleMap())));
        return await Success<VersionResponse>(await Mutate(c, venue + $"/layouts/{template.Id}/publish", HttpMethod.Post, new RevisionRequest(2)));
    }
    [Fact]
    public async Task F2_published_event_keeps_snapshot_and_resources_after_template_edits()
    {
        using var c = await Login("admin.a@demo.local"); var version = await PublishedMap(c);
        var drafts = await c.GetFromJsonAsync<List<LayoutSummary>>(A + "/layouts"); var templateId = drafts!.Single().Id;
        var before = await c.GetFromJsonAsync<LayoutResponse>(A + $"/layouts/{templateId}"); Assert.Equal(20, before!.Elements.Single(x => x.Name == "Box B3").Geometry.Rotation);
        Assert.Empty((await c.GetFromJsonAsync<List<LayoutSummary>>(A2 + "/layouts"))!);
        var draft = await Success<EventResponse>(await Mutate(c, A + "/events", HttpMethod.Post, Night(version.Id)));
        using var anon = Client(); Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/public/events/{draft.Id}")).StatusCode);
        var published = await Success<EventResponse>(await Mutate(c, A + $"/events/{draft.Id}/publish", HttpMethod.Post, new RevisionRequest(draft.Revision)));
        Assert.Equal(3, published.Resources.Count); Assert.Equal(4, published.Elements.Count); Assert.DoesNotContain(published.Resources, x => x.Name == "Barra"); Assert.Equal(1, published.Resources.Single(x => x.Name == "Box B3").AllocationLimit); Assert.Equal(80, published.Resources.Single(x => x.Name == "General").AllocationLimit);
        Assert.Equal(TimeSpan.FromHours(7), published.EndsAt - published.StartsAt); Assert.Equal(TimeSpan.Zero, published.StartsAt.Offset);
        var changed = before.Elements.Select(x => x.Name == "Box B3" ? x with { Name = "Box cambiado", Geometry = x.Geometry with { X = .2, Rotation = 90 } } : x).ToList();
        await Success<LayoutSummary>(await Mutate(c, A + $"/layouts/{templateId}", HttpMethod.Put, new LayoutSave("Mapa nuevo", 2, changed)));
        await Success<VersionResponse>(await Mutate(c, A + $"/layouts/{templateId}/publish", HttpMethod.Post, new RevisionRequest(3)));
        var persisted = await anon.GetFromJsonAsync<EventResponse>($"/api/v1/public/events/{draft.Id}"); Assert.Equal(version.Id, persisted!.LayoutVersionId); Assert.Contains(persisted.Elements, x => x.Name == "Box B3" && x.Geometry.X == .1); Assert.DoesNotContain(persisted.Elements, x => x.Name == "Box cambiado");
        Assert.Equal(HttpStatusCode.Conflict, (await Mutate(c, A + $"/events/{draft.Id}", HttpMethod.Put, Night(version.Id, published.Revision))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Mutate(c, A + $"/events/{draft.Id}", HttpMethod.Delete)).StatusCode);
    }
    [Fact]
    public async Task F2_enforces_tenant_venue_and_role_scope_for_all_new_operations()
    {
        using var a = await Login("admin.a@demo.local"); using var b = await Login("admin.b@demo.local"); using var security = await Login("security@demo.local"); using var customer = await Login("customer@demo.local");
        var version = await PublishedMap(a); var template = (await a.GetFromJsonAsync<List<LayoutSummary>>(A + "/layouts"))!.Single();
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(A + $"/layouts/{template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Mutate(b, A + $"/layouts/{template.Id}", HttpMethod.Put, new LayoutSave("Ataque", 2, SampleMap()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync(A2 + $"/layouts/{template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Mutate(a, A2 + "/events", HttpMethod.Post, Night(version.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Mutate(security, A + "/layouts", HttpMethod.Post, new LayoutCreate("Ataque"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await security.GetAsync(A2 + "/events")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Mutate(customer, A + "/events", HttpMethod.Post, Night(version.Id))).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<List<LayoutSummary>>(B + "/layouts"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync(A + "/layouts", new LayoutCreate("Sin CSRF"))).StatusCode);
    }
    [Fact]
    public async Task F2_rejects_invalid_geometry_capacity_windows_and_unpublished_maps()
    {
        using var c = await Login("admin.a@demo.local"); var t = await Success<LayoutSummary>(await Mutate(c, A + "/layouts", HttpMethod.Post, new LayoutCreate("Validación")));
        Assert.Equal(HttpStatusCode.BadRequest, (await Mutate(c, A + $"/layouts/{t.Id}/publish", HttpMethod.Post, new RevisionRequest(1))).StatusCode);
        var bad = SampleMap(); bad[0] = bad[0] with { Geometry = new(.95, .1, .2, .1, 0) };
        Assert.Equal(HttpStatusCode.BadRequest, (await Mutate(c, A + $"/layouts/{t.Id}", HttpMethod.Put, new LayoutSave("Fuera", 1, bad))).StatusCode);
        bad = SampleMap(); bad[3] = bad[3] with { Mode = "Capacity", Capacity = 1 }; Assert.Equal(HttpStatusCode.BadRequest, (await Mutate(c, A + $"/layouts/{t.Id}", HttpMethod.Put, new LayoutSave("Barra vendible", 1, bad))).StatusCode);
        var valid = SampleMap(); valid[0] = valid[0] with { Capacity = 11 }; await Success<LayoutSummary>(await Mutate(c, A + $"/layouts/{t.Id}", HttpMethod.Put, new LayoutSave("Exceso", 1, valid)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Mutate(c, A + $"/layouts/{t.Id}/publish", HttpMethod.Post, new RevisionRequest(2))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Mutate(c, A + "/events", HttpMethod.Post, Night(null, capacity: 101))).StatusCode);
        var night = Night(null); Assert.Equal(HttpStatusCode.BadRequest, (await Mutate(c, A + "/events", HttpMethod.Post, night with { EndsAt = night.StartsAt.AddHours(-1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Mutate(c, A + "/events", HttpMethod.Post, night with { RedemptionClosesAt = night.EndsAt.AddHours(1) })).StatusCode);
        var e = await Success<EventResponse>(await Mutate(c, A + "/events", HttpMethod.Post, night)); Assert.Equal(HttpStatusCode.BadRequest, (await Mutate(c, A + $"/events/{e.Id}/publish", HttpMethod.Post, new RevisionRequest(1))).StatusCode);
    }
    [Fact]
    public async Task F2_concurrent_saves_detect_conflicts_and_publication_cannot_duplicate_resources()
    {
        using var c = await Login("admin.a@demo.local"); var t = await Success<LayoutSummary>(await Mutate(c, A + "/layouts", HttpMethod.Post, new LayoutCreate("Concurrente")));
        // Separate CSRF acquisition first, so only the database operations race.
        var csrf = await c.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        HttpRequestMessage Save(string name) { var req = new HttpRequestMessage(HttpMethod.Put, A + $"/layouts/{t.Id}") { Content = JsonContent.Create(new LayoutSave(name, 1, SampleMap())) }; req.Headers.Add("X-CSRF-TOKEN", csrf!.Token); return req; }
        var writes = await Task.WhenAll(c.SendAsync(Save("Primero")), c.SendAsync(Save("Segundo"))); Assert.Single(writes, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(writes, x => x.StatusCode == HttpStatusCode.Conflict);
        var versions = await Task.WhenAll(Mutate(c, A + $"/layouts/{t.Id}/publish", HttpMethod.Post, new RevisionRequest(2)), Mutate(c, A + $"/layouts/{t.Id}/publish", HttpMethod.Post, new RevisionRequest(2)));
        var v1 = await Success<VersionResponse>(versions[0]); var v2 = await Success<VersionResponse>(versions[1]); Assert.Equal(v1.Id, v2.Id);
        var e = await Success<EventResponse>(await Mutate(c, A + "/events", HttpMethod.Post, Night(v1.Id)));
        var publications = await Task.WhenAll(Mutate(c, A + $"/events/{e.Id}/publish", HttpMethod.Post, new RevisionRequest(1)), Mutate(c, A + $"/events/{e.Id}/publish", HttpMethod.Post, new RevisionRequest(1)));
        foreach (var result in publications) Assert.Equal(3, (await Success<EventResponse>(result)).Resources.Count);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(3, await db.EventResources.CountAsync()); Assert.Equal(1, await db.LayoutVersions.CountAsync());
    }
    [Fact]
    public async Task F2_venue_and_draft_event_crud_preserves_data_and_capacity_limits()
    {
        using var c = await Login("admin.a@demo.local"); var prefix = "/api/v1/tenants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/venues";
        var venue = await Success<VenueResponse>(await Mutate(c, prefix, HttpMethod.Post, new VenueCreate("Nueva sede", 150))); var path = prefix + "/" + venue.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await Mutate(c, path, HttpMethod.Put, new VenueUpdate("Renombrada", 120))).StatusCode);
        var e = await Success<EventResponse>(await Mutate(c, path + "/events", HttpMethod.Post, Night(null)));
        var updated = await Success<EventResponse>(await Mutate(c, path + $"/events/{e.Id}", HttpMethod.Put, Night(null, 1) with { Name = "Actualizado" })); Assert.Equal("Actualizado", updated.Name);
        Assert.Equal(HttpStatusCode.Conflict, (await Mutate(c, path, HttpMethod.Put, new VenueUpdate("Límite", 90))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Mutate(c, path + $"/events/{e.Id}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(path + $"/events/{e.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Mutate(c, path, HttpMethod.Delete)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(path)).StatusCode);
        using var scope = factory.Services.CreateScope(); Assert.True((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Venues.SingleAsync(x => x.Id == venue.Id)).Archived);
    }
    [Fact]
    public async Task F2_database_rejects_event_using_a_version_from_another_venue()
    {
        using var c = await Login("admin.a@demo.local"); var version = await PublishedMap(c); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var e = new ClubEvent { Id = Guid.NewGuid(), TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), VenueId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), Name = "Referencia inválida" }; var r = Night(version.Id); e.Capacity = r.Capacity; e.StartsAt = r.StartsAt.ToUniversalTime(); e.EndsAt = r.EndsAt.ToUniversalTime(); e.EntryOpensAt = r.EntryOpensAt.ToUniversalTime(); e.EntryClosesAt = r.EntryClosesAt.ToUniversalTime(); e.RedemptionOpensAt = r.RedemptionOpensAt.ToUniversalTime(); e.RedemptionClosesAt = r.RedemptionClosesAt.ToUniversalTime(); e.LayoutVersionId = version.Id; db.Events.Add(e); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

