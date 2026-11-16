using ClubPlatform.Core;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Infrastructure;

public partial class PlanningModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<Venue>().Property(x => x.Capacity).HasDefaultValue(100);
        b.Entity<Venue>().ToTable(t => t.HasCheckConstraint("CK_Venue_Capacity", "\"Capacity\" > 0 AND \"Capacity\" <= 100000"));
        b.Entity<LayoutTemplate>().HasAlternateKey(x => new { x.TenantId, x.VenueId, x.Id });
        b.Entity<LayoutTemplate>().HasOne<Venue>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LayoutElement>().HasKey(x => new { x.TemplateId, x.Id });
        b.Entity<LayoutElement>().HasOne<LayoutTemplate>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.TemplateId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LayoutElement>().Property(x => x.GeometryJson).HasColumnType("jsonb");
        b.Entity<LayoutElement>().ToTable(t => t.HasCheckConstraint("CK_LayoutElement_Mode", "(\"Mode\" = 'Decoration' AND \"Capacity\" = 0) OR (\"Mode\" IN ('Exclusive','Capacity') AND \"Capacity\" > 0)"));
        b.Entity<LayoutVersion>().HasAlternateKey(x => new { x.TenantId, x.VenueId, x.Id });
        b.Entity<LayoutVersion>().HasOne<LayoutTemplate>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.TemplateId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LayoutVersion>().HasIndex(x => new { x.TemplateId, x.Number }).IsUnique();
        b.Entity<LayoutVersion>().HasIndex(x => new { x.TemplateId, x.SourceRevision }).IsUnique();
        b.Entity<LayoutVersion>().Property(x => x.ElementsJson).HasColumnType("jsonb");
        b.Entity<ClubEvent>().HasAlternateKey(x => new { x.TenantId, x.VenueId, x.Id });
        b.Entity<ClubEvent>().HasOne<Venue>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ClubEvent>().HasOne<LayoutVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.LayoutVersionId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ClubEvent>().Property(x => x.MapJson).HasColumnType("jsonb");
        b.Entity<ClubEvent>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Event_Status", "\"Status\" IN ('Draft','Published')");
            t.HasCheckConstraint("CK_Event_Capacity", "\"Capacity\" > 0");
            t.HasCheckConstraint("CK_Event_Times", "\"StartsAt\" < \"EndsAt\" AND \"EntryOpensAt\" < \"EntryClosesAt\" AND \"EntryClosesAt\" >= \"StartsAt\" AND \"EntryClosesAt\" <= \"EndsAt\" AND \"RedemptionOpensAt\" >= \"StartsAt\" AND \"RedemptionOpensAt\" < \"RedemptionClosesAt\" AND \"RedemptionClosesAt\" <= \"EndsAt\"");
            t.HasCheckConstraint("CK_Event_PublishedVersion", "\"Status\" <> 'Published' OR \"LayoutVersionId\" IS NOT NULL");
        });
        b.Entity<EventResource>().HasOne<ClubEvent>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EventResource>().HasIndex(x => new { x.EventId, x.SourceElementId }).IsUnique();
        b.Entity<EventResource>().ToTable(t => t.HasCheckConstraint("CK_Resource_Limits", "\"HumanCapacity\" > 0 AND ((\"Mode\" = 'Exclusive' AND \"AllocationLimit\" = 1) OR (\"Mode\" = 'Capacity' AND \"AllocationLimit\" = \"HumanCapacity\"))"));
    }
}

