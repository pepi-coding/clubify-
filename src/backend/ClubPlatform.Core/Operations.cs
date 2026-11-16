namespace ClubPlatform.Core;

public class AdmissionLog : EventEntity
{
    public Guid TicketId { get; set; }
    public Guid OrderId { get; set; }
    public string ActorId { get; set; } = "";
    public string Station { get; set; } = "";
    public DateTimeOffset AdmittedAt { get; set; }
}
public class RedemptionOperation : EventEntity
{
    public Guid VoucherId { get; set; }
    public Guid OrderId { get; set; }
    public string ActorId { get; set; } = "";
    public string Station { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public class RedemptionLine : EventEntity
{
    public Guid OperationId { get; set; }
    public Guid VoucherId { get; set; }
    public Guid EntitlementId { get; set; }
    public int Quantity { get; set; }
}
public class ScanAttempt : EventEntity
{
    public string ActorId { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Phase { get; set; } = "";
    public string Outcome { get; set; } = "";
    public Guid? SubjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
