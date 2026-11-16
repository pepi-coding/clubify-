using System.Net;
using System.Net.Http.Json;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClubPlatform.Tests;

public partial class PhaseOneTests : IAsyncLifetime
{
    private readonly string password = "Test!" + Guid.NewGuid().ToString("N");
    private readonly string database = "clubtest_" + Guid.NewGuid().ToString("N");
    private WebApplicationFactory<Program> factory = null!;
    public async Task InitializeAsync()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TestDatabase__Connection") ?? throw new InvalidOperationException("Load scripts/dev-env.ps1 to configure the local test database."));
        if (connection.Host is not ("localhost" or "127.0.0.1")) throw new InvalidOperationException("Integration tests require local PostgreSQL.");
        connection.Database = database;
        var cs = connection.ConnectionString;
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = cs, ["DataProtection:Path"] = Path.Combine(Path.GetTempPath(), "clubplatform-test-keys"), ["Payments:Provider"] = "Demo", ["Seed:Enabled"] = "false" })));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new[] { "AdminA", "AdminB", "Security", "Bar", "Customer" }.ToDictionary(x => "Seed:Passwords:" + x, _ => (string?)password)).Build();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
        await DemoSeed.Run(db, users, config); await DemoSeed.Run(db, users, config);
    }
    public async Task DisposeAsync() { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync(); await factory.DisposeAsync(); }
    private HttpClient Client() => factory.CreateClient(new() { AllowAutoRedirect = false });
    private async Task<HttpResponseMessage> Mutate(HttpClient client, string url, HttpMethod method, object? body = null)
    { var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf"); var req = new HttpRequestMessage(method, url); req.Headers.Add("X-CSRF-TOKEN", csrf!.Token); if (body != null) req.Content = JsonContent.Create(body); return await client.SendAsync(req); }
    private async Task<HttpClient> Login(string email)
    { var client = Client(); Assert.Equal(HttpStatusCode.OK, (await Mutate(client, "/api/v1/auth/login", HttpMethod.Post, new { email, password })).StatusCode); return client; }
    private const string A = "/api/v1/tenants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/venues/aaaaaaaa-0000-0000-0000-000000000001";
    private const string A2 = "/api/v1/tenants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/venues/aaaaaaaa-0000-0000-0000-000000000002";
    private const string B = "/api/v1/tenants/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/venues/bbbbbbbb-0000-0000-0000-000000000001";
    [Fact]
    public async Task Login_limits_attempts_by_account_and_by_ip()
    {
        using var c = Client();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await Mutate(c, "/api/v1/auth/login", HttpMethod.Post, new { email = "security@demo.local", password = "Incorrect!Password1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Mutate(c, "/api/v1/auth/login", HttpMethod.Post, new { email = "security@demo.local", password })).StatusCode);
        for (var i = 0; i < 14; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await Mutate(c, "/api/v1/auth/login", HttpMethod.Post, new { email = "unknown@demo.local", password })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Mutate(c, "/api/v1/auth/login", HttpMethod.Post, new { email = "unknown@demo.local", password })).StatusCode);
    }
    [Fact]
    public async Task Admin_cannot_read_or_modify_another_tenant_even_by_forging_ids()
    { using var c = await Login("admin.a@demo.local"); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(A)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(B)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await Mutate(c, B, HttpMethod.Put, new { name = "Attack" })).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(A.Replace("aaaaaaaa-0000-0000-0000-000000000001", "bbbbbbbb-0000-0000-0000-000000000001"))).StatusCode); using var other = await Login("admin.b@demo.local"); Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(A)).StatusCode); Assert.DoesNotContain("Attack", await other.GetStringAsync(B)); }
    [Theory]
    [InlineData("security@demo.local")]
    [InlineData("bar@demo.local")]
    public async Task Staff_is_limited_to_assigned_venue_and_cannot_edit(string email)
    { using var c = await Login(email); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(A)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(A2)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await Mutate(c, A2 + "/context", HttpMethod.Put)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await Mutate(c, A, HttpMethod.Put, new { name = "Attack" })).StatusCode); }
    [Fact]
    public async Task Customer_and_anonymous_have_no_administrative_access()
    { using var c = await Login("customer@demo.local"); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(A)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/platform/tenants")).StatusCode); using var anon = Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(A)).StatusCode); }
    [Fact]
    public async Task Mutations_require_csrf_and_logout_revokes_captured_cookie()
    {
        using var c = await Login("admin.a@demo.local"); Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(A, new { name = "Attack" })).StatusCode); var csrf = await c.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf"); using var bad = new HttpRequestMessage(HttpMethod.Put, A) { Content = JsonContent.Create(new { name = "Attack" }) }; bad.Headers.Add("X-CSRF-TOKEN", "wrong"); Assert.Equal(HttpStatusCode.BadRequest, (await c.SendAsync(bad)).StatusCode);
        // Capture a fresh login cookie; replay it after logout from a separate client.
        using var login = await Mutate(c, "/api/v1/auth/login", HttpMethod.Post, new { email = "admin.a@demo.local", password }); var cookie = login.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("club.session=")).Split(';')[0]; Assert.Equal(HttpStatusCode.NoContent, (await Mutate(c, "/api/v1/auth/logout", HttpMethod.Post)).StatusCode); using var replay = Client(); replay.DefaultRequestHeaders.Add("Cookie", cookie); Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync(A)).StatusCode);
    }
    [Fact]
    public async Task Context_and_venue_changes_persist_and_seed_is_idempotent()
    { using var c = await Login("admin.a@demo.local"); Assert.Equal(HttpStatusCode.NoContent, (await Mutate(c, A2 + "/context", HttpMethod.Put)).StatusCode); Assert.Contains("aaaaaaaa-0000-0000-0000-000000000002", await c.GetStringAsync("/api/v1/auth/me")); Assert.Equal(HttpStatusCode.NoContent, (await Mutate(c, A, HttpMethod.Put, new { name = "Sede actualizada" })).StatusCode); Assert.Contains("Sede actualizada", await c.GetStringAsync(A)); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(2, await db.Tenants.CountAsync()); Assert.Equal(3, await db.Venues.CountAsync()); Assert.Equal(5, await db.Users.CountAsync()); Assert.Equal(4, await db.Memberships.CountAsync()); }
    [Fact]
    public async Task Disabled_membership_and_revoked_session_take_effect_immediately()
    { using var c = await Login("security@demo.local"); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var uid = (await db.Users.SingleAsync(x => x.Email == "security@demo.local")).Id; await db.Memberships.Where(x => x.UserId == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, false)); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(A)).StatusCode); await db.Sessions.Where(x => x.UserId == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow)); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/auth/me")).StatusCode); }
    [Fact]
    public async Task Database_rejects_cross_tenant_assignments()
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var uid = (await db.Users.SingleAsync(x => x.Email == "security@demo.local")).Id; db.Assignments.Add(new() { TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), VenueId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), UserId = uid }); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Sessions.Add(new() { Id = Guid.NewGuid(), UserId = uid, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), VenueId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001") });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Health_and_api_not_found_have_honest_statuses()
    { using var c = Client(); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/live")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/ready")).StatusCode); var r = await c.GetAsync("/api/inexistente"); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); Assert.Equal("application/problem+json", r.Content.Headers.ContentType!.MediaType); }
}






