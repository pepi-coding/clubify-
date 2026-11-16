using ClubPlatform.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace ClubPlatform.Infrastructure;

public static class DemoSeed
{
    public static async Task Run(AppDbContext db, UserManager<AppUser> users, IConfiguration config)
    {
        var accounts = new List<(string, string)> { ("AdminA", "admin.a@demo.local"), ("AdminB", "admin.b@demo.local"), ("Security", "security@demo.local"), ("Bar", "bar@demo.local"), ("Customer", "customer@demo.local") };
        if (!string.IsNullOrWhiteSpace(config["Seed:Passwords:Cashier"])) accounts.Add(("Cashier", "cashier@demo.local"));
        if (!string.IsNullOrWhiteSpace(config["Seed:Passwords:Promoter"])) accounts.Add(("Promoter", "promoter@demo.local"));
        foreach (var (key, _) in accounts) if (string.IsNullOrWhiteSpace(config[$"Seed:Passwords:{key}"])) throw new InvalidOperationException($"Missing Seed:Passwords:{key}");
        await using var tx = await db.Database.BeginTransactionAsync();
        // Serialize repeat/concurrent demo bootstraps on PostgreSQL.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73492611)");
        Guid a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        if (!await db.Tenants.AnyAsync(x => x.Id == a)) db.Tenants.Add(new() { Id = a, Name = "Empresa A" });
        if (!await db.Tenants.AnyAsync(x => x.Id == b)) db.Tenants.Add(new() { Id = b, Name = "Empresa B" });
        var venues = new[] { (Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), a, "A · Miraflores"), (Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), a, "A · Barranco"), (Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), b, "B · Centro") };
        foreach (var (id, tenant, name) in venues) if (!await db.Venues.AnyAsync(x => x.Id == id)) db.Venues.Add(new() { Id = id, TenantId = tenant, Name = name });
        await db.SaveChangesAsync();
        foreach (var (key, email) in accounts)
        {
            var user = await users.FindByEmailAsync(email);
            if (user == null) { user = new() { UserName = email, Email = email }; var result = await users.CreateAsync(user, config[$"Seed:Passwords:{key}"]!); if (!result.Succeeded) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))); }
            if (key == "Customer") continue;
            var tenant = key == "AdminB" ? b : a;
            if (!await db.Memberships.AnyAsync(x => x.TenantId == tenant && x.UserId == user.Id)) db.Memberships.Add(new() { TenantId = tenant, UserId = user.Id, Role = key.StartsWith("Admin") ? StaffRoles.TenantAdmin : key == "Security" ? StaffRoles.Security : key == "Cashier" ? StaffRoles.Cashier : key == "Promoter" ? StaffRoles.Promoter : StaffRoles.BarStaff });
            await db.SaveChangesAsync();
            if (!key.StartsWith("Admin") && !await db.Assignments.AnyAsync(x => x.TenantId == a && x.VenueId == venues[0].Item1 && x.UserId == user.Id)) db.Assignments.Add(new() { TenantId = a, VenueId = venues[0].Item1, UserId = user.Id });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
    }
}
