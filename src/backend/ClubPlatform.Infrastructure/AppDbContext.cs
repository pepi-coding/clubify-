using ClubPlatform.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Infrastructure;

public class AppUser : IdentityUser { public bool IsPlatformAdmin { get; set; } }
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TenantMembership> Memberships => Set<TenantMembership>();
    public DbSet<VenueAssignment> Assignments => Set<VenueAssignment>();
    public DbSet<WebSession> Sessions => Set<WebSession>();
    public DbSet<LayoutTemplate> LayoutTemplates => Set<LayoutTemplate>();
    public DbSet<LayoutElement> LayoutElements => Set<LayoutElement>();
    public DbSet<LayoutVersion> LayoutVersions => Set<LayoutVersion>();
    public DbSet<ClubEvent> Events => Set<ClubEvent>();
    public DbSet<EventResource> EventResources => Set<EventResource>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b); b.HasDefaultSchema("app"); PlanningModel.Configure(b); CommerceModel.Configure(b); OperationsModel.Configure(b);
        b.Entity<Tenant>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Venue>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Venue>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TenantMembership>().HasKey(x => new { x.TenantId, x.UserId });
        b.Entity<TenantMembership>().ToTable(t => t.HasCheckConstraint("CK_Membership_Role", "\"Role\" IN ('TenantAdmin','VenueManager','Cashier','Promoter','Security','BarStaff')"));
        b.Entity<TenantMembership>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TenantMembership>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VenueAssignment>().HasKey(x => new { x.TenantId, x.VenueId, x.UserId });
        b.Entity<VenueAssignment>().HasOne<Venue>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VenueAssignment>().HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WebSession>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WebSession>().HasIndex(x => x.ExpiresAt);
        b.Entity<WebSession>().HasOne<Venue>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WebSession>().ToTable(t => t.HasCheckConstraint("CK_Session_Context", "(\"TenantId\" IS NULL AND \"VenueId\" IS NULL) OR (\"TenantId\" IS NOT NULL AND \"VenueId\" IS NOT NULL)"));
    }
}
