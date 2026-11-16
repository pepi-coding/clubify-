using ClubPlatform.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ClubPlatform.Infrastructure;
public static class OperationsModel
{
    private static EntityTypeBuilder<T> Event<T>(ModelBuilder b) where T : EventEntity
    {
        var e=b.Entity<T>();e.HasKey(x=>x.Id);e.HasAlternateKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.Id});
        e.HasOne<ClubEvent>().WithMany().HasForeignKey(x=>new{x.TenantId,x.VenueId,x.EventId}).HasPrincipalKey(x=>new{x.TenantId,x.VenueId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x=>new{x.TenantId,x.EventId});return e;
    }
    public static void Configure(ModelBuilder b)
    {
        b.Entity<Ticket>().HasAlternateKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.OrderId,x.Id});
        b.Entity<Ticket>().ToTable(t=>t.HasCheckConstraint("CK_Ticket_State","\"Status\" IN ('Issued','Used','Revoked')"));
        b.Entity<ConsumptionVoucher>().HasAlternateKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.OrderId,x.Id});
        b.Entity<ConsumptionVoucher>().Property(x=>x.Status).HasDefaultValue("Granted");
        b.Entity<ConsumptionVoucher>().ToTable(t=>t.HasCheckConstraint("CK_Voucher_State","\"Status\" IN ('Granted','PartiallyRedeemed','Redeemed','Revoked')"));
        b.Entity<ConsumptionEntitlement>().HasAlternateKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.VoucherId,x.Id});
        var admission=Event<AdmissionLog>(b);admission.HasIndex(x=>x.TicketId).IsUnique();
        admission.HasOne<Ticket>().WithMany().HasForeignKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.OrderId,x.TicketId}).HasPrincipalKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.OrderId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        admission.HasOne<AppUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);admission.Property(x=>x.Station).HasMaxLength(80);
        var op=Event<RedemptionOperation>(b);op.HasAlternateKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.VoucherId,x.Id});
        op.HasOne<ConsumptionVoucher>().WithMany().HasForeignKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.OrderId,x.VoucherId}).HasPrincipalKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.OrderId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        op.HasOne<AppUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);op.Property(x=>x.Station).HasMaxLength(80);
        var line=Event<RedemptionLine>(b);line.HasIndex(x=>new{x.OperationId,x.EntitlementId}).IsUnique();
        line.HasOne<RedemptionOperation>().WithMany().HasForeignKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.VoucherId,x.OperationId}).HasPrincipalKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.VoucherId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        line.HasOne<ConsumptionEntitlement>().WithMany().HasForeignKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.VoucherId,x.EntitlementId}).HasPrincipalKey(x=>new{x.TenantId,x.VenueId,x.EventId,x.VoucherId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        line.ToTable(t=>t.HasCheckConstraint("CK_RedemptionLine_Quantity","\"Quantity\" > 0"));
        var scan=Event<ScanAttempt>(b);scan.HasOne<AppUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);scan.HasIndex(x=>x.CreatedAt);
    }
}
