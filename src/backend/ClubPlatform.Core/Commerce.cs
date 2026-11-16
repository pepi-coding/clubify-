namespace ClubPlatform.Core;

public abstract class EventEntity { public Guid Id { get; set; } = Guid.NewGuid(); public Guid TenantId { get; set; } public Guid VenueId { get; set; } public Guid EventId { get; set; } }
public class Product { public Guid Id { get; set; } = Guid.NewGuid(); public Guid TenantId { get; set; } public string Name { get; set; } = ""; public bool Archived { get; set; } }
public class Offer : EventEntity { public string Name { get; set; } = ""; public bool Archived { get; set; } public int Revision { get; set; } }
public class OfferVersion : EventEntity { public Guid OfferId { get; set; } public int Number { get; set; } public string Name { get; set; } = ""; public long PriceCents { get; set; } public string Currency { get; set; } = "PEN"; public Guid? ResourceId { get; set; } public int ResourceUnits { get; set; } public int Admissions { get; set; } public string DrinksJson { get; set; } = "[]"; public DateTimeOffset CreatedAt { get; set; } }
public class EventInventory : EventEntity { public Guid? ResourceId { get; set; } public int Limit { get; set; } public int Held { get; set; } public int Sold { get; set; } public bool Blocked { get; set; } }
public class Hold : EventEntity { public string UserId { get; set; } = ""; public string Status { get; set; } = "Active"; public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset CreatedAt { get; set; } public long TotalCents { get; set; } }
public class HoldLine : EventEntity { public Guid HoldId { get; set; } public Guid OfferVersionId { get; set; } public int Quantity { get; set; } }
public class InventoryAllocation : EventEntity { public Guid HoldId { get; set; } public Guid InventoryId { get; set; } public int Quantity { get; set; } public string Status { get; set; } = "Held"; }
public class Order : EventEntity { public Guid HoldId { get; set; } public string UserId { get; set; } = ""; public string Status { get; set; } = "PendingPayment"; public string Source { get; set; } = "Online"; public string Reason { get; set; } = ""; public long TotalCents { get; set; } public string Currency { get; set; } = "PEN"; public DateTimeOffset CreatedAt { get; set; } }
public class OrderLine : EventEntity { public Guid OrderId { get; set; } public Guid OfferVersionId { get; set; } public string Name { get; set; } = ""; public long UnitPriceCents { get; set; } public int Quantity { get; set; } public int Admissions { get; set; } public string Zone { get; set; } = ""; public string DrinksJson { get; set; } = "[]"; }
public class PaymentAttempt : EventEntity { public string? PaymentMethod { get; set; } public string? ReceiptReference { get; set; } public string? VerifiedBy { get; set; } public DateTimeOffset? PaidAt { get; set; } public Guid OrderId { get; set; } public string Status { get; set; } = "Created"; public long AmountCents { get; set; } public string Currency { get; set; } = "PEN"; public string Reference { get; set; } = Guid.NewGuid().ToString("N"); public DateTimeOffset CreatedAt { get; set; } }
public class Ticket : EventEntity { public Guid OrderId { get; set; } public Guid OrderLineId { get; set; } public int Ordinal { get; set; } public string Zone { get; set; } = ""; public string Status { get; set; } = "Issued"; public string TokenHash { get; set; } = ""; public string EncryptedToken { get; set; } = ""; }
public class ConsumptionVoucher : EventEntity { public string Status { get; set; } = "Granted"; public Guid OrderId { get; set; } public string TokenHash { get; set; } = ""; public string EncryptedToken { get; set; } = ""; }
public class ConsumptionEntitlement : EventEntity { public Guid VoucherId { get; set; } public Guid ProductId { get; set; } public string Name { get; set; } = ""; public int Granted { get; set; } public int Delivered { get; set; } }
public class IdempotencyRecord : EventEntity { public string ActorId { get; set; } = ""; public string Operation { get; set; } = ""; public string Key { get; set; } = ""; public string BodyHash { get; set; } = ""; public string ResultJson { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; } }
public class AuditEntry : EventEntity { public string ActorId { get; set; } = ""; public string Action { get; set; } = ""; public string Reason { get; set; } = ""; public Guid SubjectId { get; set; } public string CorrelationId { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; } }
public record DrinkComponent(Guid ProductId, string Name, int Quantity);
public interface IPaymentGateway { string Resolve(bool approved); }
public sealed class DemoPaymentGateway : IPaymentGateway { public string Resolve(bool approved) => approved ? "Succeeded" : "Failed"; }

public class VenuePaymentSettings
{
    public Guid TenantId { get; set; }
    public Guid VenueId { get; set; }
    public string WhatsAppNumber { get; set; } = "";
    public string Instructions { get; set; } = "Paga por Yape o Plin y envía la captura por WhatsApp. El promotor verificará el abono antes de confirmar.";
}
