using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Api;

public class SessionValidation : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> context) { context.Response.StatusCode = 401; return Task.CompletedTask; }
    public override Task RedirectToAccessDenied(RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> context) { context.Response.StatusCode = 403; return Task.CompletedTask; }
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        if (!Guid.TryParse(context.Principal?.FindFirstValue("sid"), out var id) || !await db.Sessions.AnyAsync(x => x.Id == id && x.UserId == context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier) && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow)) context.RejectPrincipal();
        else { var user = await db.Users.SingleOrDefaultAsync(x => x.Id == context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)); if (user == null || user.SecurityStamp != context.Principal?.FindFirstValue("AspNet.Identity.SecurityStamp") || user.IsPlatformAdmin != context.Principal!.HasClaim("platform", "true")) context.RejectPrincipal(); }
    }
}
public record ScopeRequirement(string[] Roles) : IAuthorizationRequirement;
public class ScopeHandler(AppDbContext db) : AuthorizationHandler<ScopeRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (context.Resource is not HttpContext http || !Guid.TryParse(http.Request.RouteValues["tenantId"]?.ToString(), out var tenant)) return;
        var uid = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var membership = await db.Memberships.SingleOrDefaultAsync(x => x.TenantId == tenant && x.UserId == uid && x.Active);
        if (membership == null) { http.Items["ScopeNotFound"] = true; return; }
        if (!requirement.Roles.Contains(membership.Role)) return;
        if (Guid.TryParse(http.Request.RouteValues["venueId"]?.ToString(), out var venue))
        {
            if (!await db.Venues.AnyAsync(x => x.TenantId == tenant && x.Id == venue && !x.Archived)) { http.Items["ScopeNotFound"] = true; return; }
            if (membership.Role != StaffRoles.TenantAdmin && !await db.Assignments.AnyAsync(x => x.TenantId == tenant && x.VenueId == venue && x.UserId == uid)) return;
        }
        context.Succeed(requirement);
    }
}
public class ScopeResultHandler : Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler
{
    private readonly Microsoft.AspNetCore.Authorization.Policy.AuthorizationMiddlewareResultHandler fallback = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, Microsoft.AspNetCore.Authorization.Policy.PolicyAuthorizationResult result)
    {
        if (context.Items.ContainsKey("ScopeNotFound") && context.User.Identity?.IsAuthenticated == true) { await Results.Problem(statusCode: 404, title: "Recurso no encontrado").ExecuteAsync(context); return; }
        await fallback.HandleAsync(next, context, policy, result);
    }
}




