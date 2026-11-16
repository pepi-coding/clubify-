namespace ClubPlatform.Core;

public static class StaffRoles
{
    public const string TenantAdmin = "TenantAdmin", VenueManager = "VenueManager", Cashier = "Cashier", Promoter = "Promoter", Security = "Security", BarStaff = "BarStaff";
    public static readonly string[] All = [TenantAdmin, VenueManager, Cashier, Promoter, Security, BarStaff];
}
public class Tenant { public Guid Id { get; set; } public string Name { get; set; } = ""; }
public class Venue { public Guid Id { get; set; } public Guid TenantId { get; set; } public string Name { get; set; } = ""; public string TimeZone { get; set; } = "America/Lima"; public int Capacity { get; set; } = 100; public bool Archived { get; set; } }
public class TenantMembership { public Guid TenantId { get; set; } public string UserId { get; set; } = ""; public string Role { get; set; } = ""; public bool Active { get; set; } = true; }
public class VenueAssignment { public Guid TenantId { get; set; } public Guid VenueId { get; set; } public string UserId { get; set; } = ""; }
public class WebSession { public Guid Id { get; set; } public string UserId { get; set; } = ""; public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset? RevokedAt { get; set; } public Guid? TenantId { get; set; } public Guid? VenueId { get; set; } }

