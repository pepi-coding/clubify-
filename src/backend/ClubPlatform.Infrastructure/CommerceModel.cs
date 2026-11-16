using ClubPlatform.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ClubPlatform.Infrastructure;
public static class CommerceModel
{
    private static EntityTypeBuilder<T> Event<T>(ModelBuilder b) where T : EventEntity
    {
        var e = b.Entity<T>(); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
        e.HasOne<ClubEvent>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.TenantId, x.EventId }); return e;
    }
    public static void Configure(ModelBuilder b)
    {
        b.HasSequence<long>("OrderReferenceNumbers", "app").StartsAt(1).HasMax(1099511627775L);
        b.Entity<Order>().Property(x => x.Reference).HasMaxLength(9).HasDefaultValueSql("app.next_order_reference()");
        b.Entity<Order>().Property(x => x.Reference).Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Throw);
        b.Entity<Order>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<Order>().ToTable(t => t.HasCheckConstraint("CK_Order_Reference", "\"Reference\" ~ '^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$'"));
        var settings = b.Entity<VenuePaymentSettings>(); settings.HasKey(x => new { x.TenantId, x.VenueId });
        settings.HasOne<Venue>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        settings.Property(x => x.WhatsAppNumber).HasMaxLength(15); settings.Property(x => x.Instructions).HasMaxLength(1000);
        b.Entity<PaymentAttempt>().HasIndex(x => new { x.TenantId, x.PaymentMethod, x.ReceiptReference }).IsUnique();
        b.Entity<PaymentAttempt>().Property(x => x.ReceiptReference).HasMaxLength(80);
        b.Entity<Product>().HasAlternateKey(x => new { x.TenantId, x.Id }); b.Entity<Product>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        Event<Offer>(b); var ov = Event<OfferVersion>(b);
        ov.HasOne<Offer>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.OfferId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EventResource>().HasAlternateKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id });
        ov.HasOne<EventResource>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.ResourceId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        ov.HasIndex(x => new { x.OfferId, x.Number }).IsUnique(); ov.Property(x => x.DrinksJson).HasColumnType("jsonb");
        ov.ToTable(t => t.HasCheckConstraint("CK_Offer_Amounts", "\"PriceCents\" BETWEEN 0 AND 100000000 AND \"Admissions\" BETWEEN 0 AND 1000 AND \"ResourceUnits\" BETWEEN 0 AND 1000 AND \"Currency\" = 'PEN'"));
        var inv = Event<EventInventory>(b); inv.HasIndex(x => new { x.EventId, x.ResourceId }).IsUnique().AreNullsDistinct(false);
        inv.HasOne<EventResource>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.ResourceId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        inv.ToTable(t => t.HasCheckConstraint("CK_Inventory_Counts", "\"Limit\" >= 0 AND \"Held\" >= 0 AND \"Sold\" >= 0 AND \"Held\" + \"Sold\" <= \"Limit\""));
        var h = Event<Hold>(b); h.HasIndex(x => new { x.Status, x.ExpiresAt }); h.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        h.ToTable(t => t.HasCheckConstraint("CK_Hold_State", "\"Status\" IN ('Active','Converted','Expired','Cancelled') AND \"TotalCents\" >= 0"));
        var hl = Event<HoldLine>(b); hl.HasOne<Hold>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.HoldId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        hl.HasOne<OfferVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.OfferVersionId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        hl.ToTable(t => t.HasCheckConstraint("CK_HoldLine_Quantity", "\"Quantity\" BETWEEN 1 AND 100"));
        var al = Event<InventoryAllocation>(b); al.HasIndex(x => new { x.HoldId, x.InventoryId }).IsUnique();
        al.HasOne<Hold>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.HoldId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        al.HasOne<EventInventory>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.InventoryId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        al.ToTable(t => t.HasCheckConstraint("CK_Allocation", "\"Quantity\" > 0 AND \"Status\" IN ('Held','Sold','Released')"));
        var o = Event<Order>(b); o.HasIndex(x => x.HoldId).IsUnique(); o.HasOne<Hold>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.HoldId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); o.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        o.ToTable(t => t.HasCheckConstraint("CK_Order_State", "\"Status\" IN ('PendingPayment','Confirmed','Expired','Cancelled','PaymentReview') AND \"TotalCents\" >= 0"));
        var ol = Event<OrderLine>(b); ol.Property(x => x.DrinksJson).HasColumnType("jsonb"); ol.HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        ol.HasOne<OfferVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.OfferVersionId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var p = Event<PaymentAttempt>(b); p.HasIndex(x => x.Reference).IsUnique(); p.HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var tk = Event<Ticket>(b); tk.HasIndex(x => new { x.OrderId, x.OrderLineId, x.Ordinal }).IsUnique(); tk.HasIndex(x => x.TokenHash).IsUnique(); tk.HasOne<OrderLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.OrderLineId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); tk.HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var v = Event<ConsumptionVoucher>(b); v.HasIndex(x => x.OrderId).IsUnique(); v.HasIndex(x => x.TokenHash).IsUnique(); v.HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var ent = Event<ConsumptionEntitlement>(b); ent.HasIndex(x => new { x.VoucherId, x.ProductId }).IsUnique(); ent.HasOne<ConsumptionVoucher>().WithMany().HasForeignKey(x => new { x.TenantId, x.VenueId, x.EventId, x.VoucherId }).HasPrincipalKey(x => new { x.TenantId, x.VenueId, x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); ent.HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); ent.ToTable(t => t.HasCheckConstraint("CK_Entitlement", "\"Granted\" > 0 AND \"Delivered\" >= 0 AND \"Delivered\" <= \"Granted\""));
        var idem = Event<IdempotencyRecord>(b); idem.HasIndex(x => new { x.ActorId, x.TenantId, x.Operation, x.Key }).IsUnique(); idem.Property(x => x.ResultJson).HasColumnType("jsonb"); Event<AuditEntry>(b);
    }
}
