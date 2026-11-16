using System.Security.Claims;
using System.Text.RegularExpressions;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace ClubPlatform.Api;

public record TokenRequest(string Token);
public record AdmissionRequest(string Token, string Station = "");
public record RedemptionSelection(Guid EntitlementId, int Quantity);
public record RedemptionRequest(string Token, List<RedemptionSelection> Lines, string Station = "");
public record ConsumptionBalance(Guid Id, string Name, int Granted, int Delivered, int Remaining);
public record ScanOutcome(string Code, string Message, bool CanConfirm = false, Guid? SubjectId = null, string? Zone = null, int? Ordinal = null, DateTimeOffset? RecordedAt = null, List<ConsumptionBalance>? Lines = null, string? State = null, Guid? OperationId = null);

public sealed partial class CommerceService
{
    private static bool TokenFormat(string? token) => token != null && Regex.IsMatch(token, "^[tv]_[A-F0-9]{64}$", RegexOptions.CultureInvariant);
    private void Scan(ClubEvent e, string actor, string channel, bool confirm, ScanOutcome outcome)
    {
        db.Add(Scope(new ScanAttempt { ActorId=actor,Channel=channel,Phase=confirm?"Confirm":"Inspect",Outcome=outcome.Code,SubjectId=outcome.SubjectId,CreatedAt=Now },e));
    }
    public async Task<ScanOutcome> Admission(ClubEvent e, string actor, string? token, bool confirm, string? station = "")
    {
        if (station?.Length > 80) Fail(422,"INVALID_STATION","La estación admite hasta 80 caracteres.");
        token=token?.Trim();
        ScanOutcome result;
        if (!TokenFormat(token)) result=new("INVALID","Código inválido o de otro evento/sede.");
        else if (!token!.StartsWith("t_",StringComparison.Ordinal)) result=new("WRONG_CREDENTIAL_TYPE","Un vale de consumo no permite ingresar. Usa un pase individual.");
        else {
            var hash=Hash(token);
            var ticket=await db.Set<Ticket>().FromSqlInterpolated($"SELECT * FROM app.\"Ticket\" WHERE \"TenantId\"={e.TenantId} AND \"VenueId\"={e.VenueId} AND \"EventId\"={e.Id} AND \"TokenHash\"={hash} FOR UPDATE").SingleOrDefaultAsync();
            if (ticket==null) result=new("INVALID","Código inválido o de otro evento/sede.");
            else {
                var admitted=await db.Set<AdmissionLog>().SingleOrDefaultAsync(x=>x.TicketId==ticket.Id&&x.TenantId==e.TenantId&&x.EventId==e.Id);
                var confirmed=await db.Set<Order>().AnyAsync(x=>x.Id==ticket.OrderId&&x.TenantId==e.TenantId&&x.EventId==e.Id&&x.Status=="Confirmed");
                if (ticket.Status=="Revoked"||!confirmed) result=new("REVOKED","Pase revocado o pedido no confirmado.",SubjectId:ticket.Id);
                else if(ticket.Status=="Used"||admitted!=null) result=new("ALREADY_USED","Este pase ya registró un ingreso. No permite reingreso.",SubjectId:ticket.Id,Zone:ticket.Zone,Ordinal:ticket.Ordinal,RecordedAt:admitted?.AdmittedAt,State:"Used");
                else if(Now<e.EntryOpensAt||Now>=e.EntryClosesAt) result=new("OUTSIDE_WINDOW","Fuera del horario de ingreso de este evento.",SubjectId:ticket.Id,State:ticket.Status);
                else if(ticket.Status!="Issued") result=new("INVALID","Pase sin estado válido para ingreso.",SubjectId:ticket.Id);
                else if(!confirm) result=new("VALID","Pase válido. Falta confirmar el ingreso.",true,ticket.Id,ticket.Zone,ticket.Ordinal,State:"Issued");
                else {
                    var log=Scope(new AdmissionLog{TicketId=ticket.Id,OrderId=ticket.OrderId,ActorId=actor,Station=station?.Trim()??"",AdmittedAt=Now},e);db.Add(log);ticket.Status="Used";
                    Audit(e,actor,"AdmissionRecorded",log.Id,"Ingreso sin reingreso; estación: "+log.Station);
                    result=new("ADMITTED","Ingreso confirmado.",SubjectId:ticket.Id,Zone:ticket.Zone,Ordinal:ticket.Ordinal,RecordedAt:log.AdmittedAt,State:"Used",OperationId:log.Id);
                }
            }
        }
        Scan(e,actor,"Admission",confirm,result);return result;
    }
    public async Task<ScanOutcome> Redemption(ClubEvent e, string actor, string? token, bool confirm, List<RedemptionSelection>? selections = null, string? station = "")
    {
        if(station?.Length>80) Fail(422,"INVALID_STATION","La estación admite hasta 80 caracteres.");
        if(confirm&&(selections==null||selections.Count<1||selections.Count>20||selections.Any(x=>x.Quantity<=0||x.Quantity>1000)||selections.Select(x=>x.EntitlementId).Distinct().Count()!=selections.Count)) Fail(422,"INVALID_REDEMPTION_LINES","Selecciona de 1 a 20 líneas distintas con cantidades enteras positivas de hasta 1000.");
        token=token?.Trim();ScanOutcome result;
        if(!TokenFormat(token)) result=new("INVALID","Código inválido o de otro evento/sede.");
        else if(!token!.StartsWith("v_",StringComparison.Ordinal)) result=new("WRONG_CREDENTIAL_TYPE","Un pase de ingreso no permite canjear. Usa el vale de consumo.");
        else {
            var hash=Hash(token);
            var voucher=await db.Set<ConsumptionVoucher>().FromSqlInterpolated($"SELECT * FROM app.\"ConsumptionVoucher\" WHERE \"TenantId\"={e.TenantId} AND \"VenueId\"={e.VenueId} AND \"EventId\"={e.Id} AND \"TokenHash\"={hash} FOR UPDATE").SingleOrDefaultAsync();
            if(voucher==null) result=new("INVALID","Código inválido o de otro evento/sede.");
            else {
                var entitlements=await db.Set<ConsumptionEntitlement>().FromSqlInterpolated($"SELECT * FROM app.\"ConsumptionEntitlement\" WHERE \"TenantId\"={e.TenantId} AND \"VenueId\"={e.VenueId} AND \"EventId\"={e.Id} AND \"VoucherId\"={voucher.Id} ORDER BY \"Id\" FOR UPDATE").ToListAsync();
                List<ConsumptionBalance> Balances()=>entitlements.Select(x=>new ConsumptionBalance(x.Id,x.Name,x.Granted,x.Delivered,x.Granted-x.Delivered)).ToList();
                ScanOutcome View(string code,string message,bool can=false,DateTimeOffset? at=null,Guid? op=null)=>new(code,message,can,voucher.Id,RecordedAt:at,Lines:Balances(),State:voucher.Status,OperationId:op);
                var confirmed=await db.Set<Order>().AnyAsync(x=>x.Id==voucher.OrderId&&x.TenantId==e.TenantId&&x.EventId==e.Id&&x.Status=="Confirmed");
                if(voucher.Status=="Revoked"||!confirmed) result=new("REVOKED","Vale revocado o pedido no confirmado.",SubjectId:voucher.Id,State:"Revoked");
                else if(Now<e.RedemptionOpensAt||Now>=e.RedemptionClosesAt) result=View("OUTSIDE_WINDOW","Fuera del horario de canje de este evento.");
                else if(!await db.Set<AdmissionLog>().AnyAsync(x=>x.TenantId==e.TenantId&&x.VenueId==e.VenueId&&x.EventId==e.Id&&x.OrderId==voucher.OrderId)) result=View("ENTRY_REQUIRED","Debe registrarse al menos un ingreso de este pedido antes del canje.");
                else if(entitlements.All(x=>x.Delivered==x.Granted)||voucher.Status=="Redeemed") result=View("ALREADY_REDEEMED","Este vale ya fue entregado por completo.");
                else if(!confirm) result=View("VALID","Vale válido. Selecciona cantidades y confirma la entrega.",true);
                else if(selections!.Any(s=>!entitlements.Any(x=>x.Id==s.EntitlementId&&x.Granted-x.Delivered>=s.Quantity))) result=View("BALANCE_CHANGED","La selección supera el saldo o contiene una línea ajena. Consulta el vale otra vez.");
                else {
                    var operation=Scope(new RedemptionOperation{VoucherId=voucher.Id,OrderId=voucher.OrderId,ActorId=actor,Station=station?.Trim()??"",CreatedAt=Now},e);db.Add(operation);
                    foreach(var selection in selections!) {var line=entitlements.Single(x=>x.Id==selection.EntitlementId);line.Delivered+=selection.Quantity;db.Add(Scope(new RedemptionLine{OperationId=operation.Id,VoucherId=voucher.Id,EntitlementId=line.Id,Quantity=selection.Quantity},e));}
                    voucher.Status=entitlements.All(x=>x.Delivered==x.Granted)?"Redeemed":"PartiallyRedeemed";
                    Audit(e,actor,"ConsumptionRedeemed",operation.Id,"Entrega parcial/total; estación: "+operation.Station);
                    result=View("REDEEMED","Entrega confirmada. Revisa el saldo actualizado.",at:operation.CreatedAt,op:operation.Id);
                }
            }
        }
        Scan(e,actor,"Redemption",confirm,result);return result;
    }
}
public static class OperationsEndpoints
{
    private static string Actor(HttpContext c)=>c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static void Scope(ClubEvent e,Guid t,Guid v){if(e.TenantId!=t||e.VenueId!=v)throw new CommerceException(404,"EVENT_NOT_FOUND","Evento no encontrado.");}
    private static async Task<IResult> Run(Func<Task<ScanOutcome>> action,bool confirm)
    {
        try {var result=await action();return !confirm||result.Code is "ADMITTED" or "REDEEMED"?Results.Ok(result):Results.Problem(statusCode:409,title:result.Message,extensions:new Dictionary<string,object?>{["code"]=result.Code,["recordedAt"]=result.RecordedAt,["traceId"]=System.Diagnostics.Activity.Current?.TraceId.ToString()});}
        catch(CommerceException ex){return Results.Problem(statusCode:ex.Status,title:ex.Message,extensions:new Dictionary<string,object?>{["code"]=ex.Code,["traceId"]=System.Diagnostics.Activity.Current?.TraceId.ToString()});}
    }
    public static void MapOperations(this WebApplication app)
    {
        var root=app.MapGroup("/api/v1/tenants/{tenantId:guid}/venues/{venueId:guid}/events/{eventId:guid}").RequireRateLimiting("operations");
        root.MapGet("/admissions/operations/{key}",(Guid tenantId,Guid venueId,Guid eventId,string key,HttpContext c,CommerceService s,AppDbContext db)=>Run(()=>s.Transaction(eventId,async e=>{Scope(e,tenantId,venueId);var result=await db.Set<IdempotencyRecord>().SingleOrDefaultAsync(x=>x.TenantId==e.TenantId&&x.VenueId==e.VenueId&&x.EventId==e.Id&&x.ActorId==Actor(c)&&x.Operation=="admission"&&x.Key==key);if(result==null)throw new CommerceException(404,"OPERATION_NOT_RECORDED","Aún no se encontró la confirmación. Reintenta la misma operación.");return System.Text.Json.JsonSerializer.Deserialize<ScanOutcome>(result.ResultJson,CommerceService.Json)!;}),true)).RequireAuthorization("Admission").Produces<ScanOutcome>().ProducesProblem(409);
        root.MapGet("/redemptions/operations/{key}",(Guid tenantId,Guid venueId,Guid eventId,string key,HttpContext c,CommerceService s,AppDbContext db)=>Run(()=>s.Transaction(eventId,async e=>{Scope(e,tenantId,venueId);var result=await db.Set<IdempotencyRecord>().SingleOrDefaultAsync(x=>x.TenantId==e.TenantId&&x.VenueId==e.VenueId&&x.EventId==e.Id&&x.ActorId==Actor(c)&&x.Operation=="redemption"&&x.Key==key);if(result==null)throw new CommerceException(404,"OPERATION_NOT_RECORDED","Aún no se encontró la confirmación. Reintenta la misma operación.");return System.Text.Json.JsonSerializer.Deserialize<ScanOutcome>(result.ResultJson,CommerceService.Json)!;}),true)).RequireAuthorization("Redemption").Produces<ScanOutcome>().ProducesProblem(409);
        root.MapPost("/admissions/inspect",(Guid tenantId,Guid venueId,Guid eventId,TokenRequest r,HttpContext c,CommerceService s)=>Run(()=>s.Transaction(eventId,e=>{Scope(e,tenantId,venueId);return s.Admission(e,Actor(c),r.Token,false);}),false)).RequireAuthorization("Admission").Produces<ScanOutcome>();
        root.MapPost("/admissions/check-in",(Guid tenantId,Guid venueId,Guid eventId,AdmissionRequest r,HttpContext c,CommerceService s)=>Run(()=>s.Transaction(eventId,e=>{Scope(e,tenantId,venueId);return s.Admission(e,Actor(c),r.Token,true,r.Station);},Actor(c),"admission",c.Request.Headers["Idempotency-Key"].ToString(),new{eventId,request=r},expectedTenant:tenantId,expectedVenue:venueId),true)).RequireAuthorization("Admission").Produces<ScanOutcome>().ProducesProblem(409);
        root.MapPost("/redemptions/inspect",(Guid tenantId,Guid venueId,Guid eventId,TokenRequest r,HttpContext c,CommerceService s)=>Run(()=>s.Transaction(eventId,e=>{Scope(e,tenantId,venueId);return s.Redemption(e,Actor(c),r.Token,false);}),false)).RequireAuthorization("Redemption").Produces<ScanOutcome>();
        root.MapPost("/redemptions",(Guid tenantId,Guid venueId,Guid eventId,RedemptionRequest r,HttpContext c,CommerceService s)=>Run(()=>s.Transaction(eventId,e=>{Scope(e,tenantId,venueId);return s.Redemption(e,Actor(c),r.Token,true,r.Lines,r.Station);},Actor(c),"redemption",c.Request.Headers["Idempotency-Key"].ToString(),new{eventId,request=r},expectedTenant:tenantId,expectedVenue:venueId),true)).RequireAuthorization("Redemption").Produces<ScanOutcome>().ProducesProblem(409);
    }
}
