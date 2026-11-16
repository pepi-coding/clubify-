using System.Security.Claims;
using System.Threading.RateLimiting;
using ClubPlatform.Api;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
var local = builder.Environment.IsDevelopment();
var demo = local || builder.Environment.IsEnvironment("Demo");
builder.Services.AddDataProtection().SetApplicationName("ClubPlatform").AddKeyManagementOptions(o =>
{
    var path = builder.Configuration["DataProtection:Path"] ?? throw new InvalidOperationException("DataProtection:Path must name persistent storage.");
    o.XmlRepository = new Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository(new DirectoryInfo(path), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
});
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Database connection required.")));
builder.Services.AddIdentity<AppUser, IdentityRole>(o => { o.Password.RequiredLength = 12; o.User.RequireUniqueEmail = true; o.Lockout.MaxFailedAccessAttempts = 5; }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<SessionValidation>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CommerceService>();
builder.Services.AddHostedService<HoldExpiryWorker>();
builder.Services.ConfigureApplicationCookie(o => { o.Cookie.Name = "club.session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; o.EventsType = typeof(SessionValidation); o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false; });
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Admission", p => p.RequireAuthenticatedUser().AddRequirements(new ScopeRequirement([StaffRoles.Security])));
    o.AddPolicy("Redemption", p => p.RequireAuthenticatedUser().AddRequirements(new ScopeRequirement([StaffRoles.BarStaff])));
    o.AddPolicy("TenantAdmin", p => p.RequireAuthenticatedUser().AddRequirements(new ScopeRequirement([StaffRoles.TenantAdmin])));
    o.AddPolicy("PaymentVerifier", p => p.RequireAuthenticatedUser().AddRequirements(new ScopeRequirement([StaffRoles.TenantAdmin, StaffRoles.VenueManager, StaffRoles.Promoter])));
    o.AddPolicy("Sales", p => p.RequireAuthenticatedUser().AddRequirements(new ScopeRequirement([StaffRoles.TenantAdmin, StaffRoles.VenueManager, StaffRoles.Cashier])));
    o.AddPolicy("Staff", p => p.RequireAuthenticatedUser().AddRequirements(new ScopeRequirement(StaffRoles.All)));
    o.AddPolicy("VenueAdmin", p => p.RequireAuthenticatedUser().AddRequirements(new ScopeRequirement([StaffRoles.TenantAdmin, StaffRoles.VenueManager])));
    o.AddPolicy("Platform", p => p.RequireAuthenticatedUser().RequireClaim("platform", "true"));
});
builder.Services.AddScoped<IAuthorizationHandler, ScopeHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ScopeResultHandler>();
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("operations", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 180, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); o.AddPolicy("commerce", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);
builder.Services.AddProblemDetails(); builder.Services.AddOpenApi();
var app = builder.Build();
if (!demo && app.Configuration["Payments:Provider"] == "Demo") throw new InvalidOperationException("Demo payments forbidden outside Development/Demo.");
if (!demo && app.Configuration.GetValue<bool>("Seed:Enabled")) throw new InvalidOperationException("Demo seed forbidden outside Development/Demo.");
if (string.IsNullOrWhiteSpace(app.Configuration["DataProtection:Path"])) throw new InvalidOperationException("DataProtection:Path must name persistent storage.");
app.UseExceptionHandler(); app.UseStatusCodePages(async c => await Results.Problem(statusCode: c.HttpContext.Response.StatusCode).ExecuteAsync(c.HttpContext));
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method))
    { try { await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx); } catch (AntiforgeryValidationException) { await Results.Problem(statusCode: 400, title: "Token CSRF inválido").ExecuteAsync(ctx); return; } }
    await next();
});
app.MapOpenApi();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (AppDbContext db) => { try { return await db.Database.CanConnectAsync() && !(await db.Database.GetPendingMigrationsAsync()).Any() ? Results.Ok(new { status = "ready" }) : Results.Problem(statusCode: 503, title: "Base de datos no disponible"); } catch { return Results.Problem(statusCode: 503, title: "Base de datos no disponible"); } });
var api = app.MapGroup("/api/v1");
api.MapGet("/auth/csrf", (IAntiforgery csrf, HttpContext ctx) => new CsrfResponse(csrf.GetAndStoreTokens(ctx).RequestToken!));
api.MapPost("/auth/login", async (LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn, AppDbContext db, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Email.Length > 254 || request.Password.Length > 256) return Results.BadRequest();
    var user = await users.FindByEmailAsync(request.Email);
    if (user == null || !(await signIn.CheckPasswordSignInAsync(user, request.Password, true)).Succeeded) return Results.Problem(statusCode: 401, title: "Credenciales inválidas");
    var session = new WebSession { Id = Guid.NewGuid(), UserId = user.Id, ExpiresAt = DateTimeOffset.UtcNow.AddHours(8) }; db.Sessions.Add(session); await db.SaveChangesAsync();
    var principal = await signIn.CreateUserPrincipalAsync(user); var identity = (ClaimsIdentity)principal.Identity!; identity.AddClaim(new("sid", session.Id.ToString())); if (user.IsPlatformAdmin) identity.AddClaim(new("platform", "true"));
    await ctx.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties { ExpiresUtc = session.ExpiresAt });
    return Results.Ok(new { email = user.Email });
}).RequireRateLimiting("login");
api.MapPost("/auth/logout", async (HttpContext ctx, AppDbContext db) => { var sid = Guid.Parse(ctx.User.FindFirstValue("sid")!); await db.Sessions.Where(x => x.Id == sid).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow)); await ctx.SignOutAsync(IdentityConstants.ApplicationScheme); return Results.NoContent(); }).RequireAuthorization();
api.MapGet("/auth/me", async (HttpContext ctx, AppDbContext db) =>
{
    var uid = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var memberships = await (from m in db.Memberships join t in db.Tenants on m.TenantId equals t.Id where m.UserId == uid && m.Active select new MembershipResponse(t.Id, t.Name, m.Role)).ToListAsync();
    var venues = await (from v in db.Venues join m in db.Memberships on v.TenantId equals m.TenantId where !v.Archived && m.UserId == uid && m.Active && (m.Role == StaffRoles.TenantAdmin || db.Assignments.Any(a => a.TenantId == v.TenantId && a.VenueId == v.Id && a.UserId == uid)) select new VenueSummary(v.Id, v.TenantId, v.Name)).ToListAsync();
    var session = await db.Sessions.SingleAsync(x => x.Id == Guid.Parse(ctx.User.FindFirstValue("sid")!));
    return Results.Ok(new MeResponse(ctx.User.Identity!.Name!, ctx.User.HasClaim("platform", "true"), memberships, venues, new ContextResponse(session.TenantId, session.VenueId)));
}).RequireAuthorization().Produces<MeResponse>();
api.MapPut("/tenants/{tenantId:guid}/venues/{venueId:guid}/context", async (Guid tenantId, Guid venueId, HttpContext ctx, AppDbContext db) => { var sid = Guid.Parse(ctx.User.FindFirstValue("sid")!); await db.Sessions.Where(x => x.Id == sid).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId).SetProperty(x => x.VenueId, venueId)); return Results.NoContent(); }).RequireAuthorization("Staff");
api.MapGet("/tenants/{tenantId:guid}/venues/{venueId:guid}", async (Guid tenantId, Guid venueId, AppDbContext db) => Results.Ok(await db.Venues.Where(x => x.TenantId == tenantId && x.Id == venueId).Select(x => new VenueResponse(x.Id, x.TenantId, x.Name, x.TimeZone, x.Capacity)).SingleAsync())).RequireAuthorization("Staff").Produces<VenueResponse>();

api.MapGet("/platform/tenants", async (AppDbContext db) => await db.Tenants.Select(x => new { x.Id, x.Name }).ToListAsync()).RequireAuthorization("Platform");
app.MapPlanning();
app.MapCommerce(); app.MapManualPayments(); app.MapOperations();
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Ruta no encontrada"));
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapFallbackToFile("index.html");
if (args.Contains("--migrate")) { using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(); return; }
if (builder.Configuration.GetValue<bool>("Seed:Enabled")) { using var scope = app.Services.CreateScope(); await DemoSeed.Run(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(), builder.Configuration); await CommerceSeed.Run(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
app.Run();
public record LoginRequest(string Email, string Password);
public record CsrfResponse(string Token);
public record VenueResponse(Guid Id, Guid TenantId, string Name, string TimeZone, int Capacity);
public record VenueUpdate(string Name, int? Capacity = null);
public partial class Program { }


public record MembershipResponse(Guid TenantId, string Name, string Role);
public record VenueSummary(Guid Id, Guid TenantId, string Name);
public record ContextResponse(Guid? TenantId, Guid? VenueId);
public record MeResponse(string Email, bool IsPlatformAdmin, List<MembershipResponse> Memberships, List<VenueSummary> Venues, ContextResponse Context);
