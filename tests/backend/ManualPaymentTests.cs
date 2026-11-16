using System.Net;
using System.Net.Http.Json;
using ClubPlatform.Api;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClubPlatform.Tests;
public partial class PhaseOneTests
{
    private WebApplicationFactory<Program> ManualFactory() => factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_,c) => c.AddInMemoryCollection(new Dictionary<string,string?> { ["Payments:Provider"]="WhatsAppManual" })));
    private async Task<HttpClient> LoginOn(WebApplicationFactory<Program> host,string email) { var c=host.CreateClient(new() {AllowAutoRedirect=false});Assert.True((await Mutate(c,"/api/v1/auth/login",HttpMethod.Post,new {email,password})).IsSuccessStatusCode);return c; }
    private async Task ManualSetup(HttpClient admin)
    {
        await SalesCatalog(admin);
        Assert.True((await Mutate(admin,A+"/payment-settings",HttpMethod.Put,new PaymentSettingsRequest("+51 900 000 000","Paga por Yape o Plin y envía la captura por WhatsApp."))).IsSuccessStatusCode);
        using var scope=factory.Services.CreateScope();var config=new ConfigurationBuilder().AddInMemoryCollection(new[]{"AdminA","AdminB","Security","Bar","Customer","Promoter"}.ToDictionary(x=>"Seed:Passwords:"+x,_=>(string?)password)).Build();await DemoSeed.Run(scope.ServiceProvider.GetRequiredService<AppDbContext>(),scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>(),config);
    }
    private async Task<ReservationDto> WhatsAppReserve(HttpClient c,OfferDto offer,string? key=null) => await Success<ReservationDto>(await Business(c,"/api/v1/reservations",Select(offer),key));
    private static string VerifyUrl(Guid id,string venue=A) => venue+$"/events/{SalesEvent}/manual-payments/{id}/verify";
    private static ManualPaymentRequest Verified(long amount,string? reference=null,bool approved=true) => new(approved,"Yape",reference??Guid.NewGuid().ToString("N"),amount,DateTimeOffset.UtcNow,"Abono revisado en la cuenta de Yape.");
    [Fact] public async Task WhatsApp_reservation_is_atomic_one_hour_idempotent_and_requires_configured_phone()
    {
        using var host=ManualFactory();using var admin=await LoginOn(host,"admin.a@demo.local");using var c=await LoginOn(host,"customer@demo.local");var cat=await SalesCatalog(c);var box=cat.Offers.Single(x=>x.Name.StartsWith("Box B3"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Business(c,"/api/v1/reservations",Select(box))).StatusCode);
        await ManualSetup(admin);var r=await WhatsAppReserve(c,box,"reserve-whatsapp");var again=await WhatsAppReserve(c,box,"reserve-whatsapp");Assert.Equal(r.OrderId,again.OrderId);Assert.Equal(r.HoldId,again.HoldId);Assert.InRange((r.Payment.ExpiresAt-r.Payment.ServerTime).TotalMinutes,59.9,60.01);Assert.StartsWith("https://wa.me/51900000000?text=",r.Payment.WhatsAppUrl);var message=Uri.UnescapeDataString(r.Payment.WhatsAppUrl!);Assert.Contains(r.OrderId.ToString(),message);Assert.Contains("1000.00",message);Assert.DoesNotContain("customer@",message);
        Assert.Equal(HttpStatusCode.Conflict,(await Business(c,"/api/v1/reservations",Select(box,2),"reserve-whatsapp")).StatusCode);
        var detail=await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{r.OrderId}");Assert.Equal("WhatsAppManual",detail!.Source);Assert.Empty(detail.Tickets);Assert.Null(detail.Voucher);Assert.NotNull(detail.Payment);
        Assert.Equal(HttpStatusCode.Forbidden,(await Business(c,$"/api/v1/demo/orders/{r.OrderId}/payment-result",new DemoPaymentRequest(true))).StatusCode);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(1,await db.Set<Order>().CountAsync(x=>x.HoldId==r.HoldId));
    }
    [Fact] public async Task Promoter_verification_is_scoped_rejects_wrong_amount_and_issues_once_under_concurrency()
    {
        using var host=ManualFactory();using var a=await LoginOn(host,"admin.a@demo.local");await ManualSetup(a);using var c=await LoginOn(host,"customer@demo.local");using var p=await LoginOn(host,"promoter@demo.local");using var b=await LoginOn(host,"admin.b@demo.local");using var security=await LoginOn(host,"security@demo.local");var box=(await SalesCatalog(c)).Offers.Single(x=>x.Name.StartsWith("Box B3"));var r=await WhatsAppReserve(c,box);
        Assert.Equal(HttpStatusCode.NotFound,(await Business(c,VerifyUrl(r.OrderId),Verified(r.TotalCents))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Business(b,VerifyUrl(r.OrderId),Verified(r.TotalCents))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Business(security,VerifyUrl(r.OrderId),Verified(r.TotalCents))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Business(p,VerifyUrl(r.OrderId,A2),Verified(r.TotalCents))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Mutate(p,A+"/payment-settings",HttpMethod.Put,new PaymentSettingsRequest("51900000001","Cambio no permitido"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Business(p,VerifyUrl(r.OrderId),Verified(1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await p.PostAsJsonAsync(VerifyUrl(r.OrderId),Verified(r.TotalCents))).StatusCode);
        var input=Verified(r.TotalCents);var responses=await Task.WhenAll(Business(p,VerifyUrl(r.OrderId),input,"verify1"),Business(p,VerifyUrl(r.OrderId),input,"verify2"));Assert.All(responses,x=>Assert.True(x.IsSuccessStatusCode));
        var detail=await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{r.OrderId}");Assert.Equal("Confirmed",detail!.Status);Assert.Equal(10,detail.Tickets.Count);Assert.Equal(3,detail.Voucher!.Lines.Sum(x=>x.Granted));
        Assert.Equal(HttpStatusCode.Conflict,(await Business(p,VerifyUrl(r.OrderId),input with {Note="Cambio"},"verify1")).StatusCode);
        var queue=await p.GetFromJsonAsync<List<PaymentQueueItem>>(A+$"/events/{SalesEvent}/manual-payments");Assert.Single(queue!);Assert.Single(queue![0].Verifications);Assert.Equal(input.ReceiptReference!.ToUpperInvariant(),queue[0].Verifications[0].ReceiptReference);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.True(await db.Set<AuditEntry>().AnyAsync(x=>x.SubjectId==r.OrderId&&x.Action=="ManualPaymentSucceeded"&&x.Reason==input.Note));Assert.NotNull((await db.Set<PaymentAttempt>().SingleAsync(x=>x.OrderId==r.OrderId)).VerifiedBy);
    }
    [Fact] public async Task Manual_rejected_proof_can_be_corrected_and_receipt_cannot_pay_another_order()
    {
        using var host=ManualFactory();using var a=await LoginOn(host,"admin.a@demo.local");await ManualSetup(a);using var c=await LoginOn(host,"customer@demo.local");using var p=await LoginOn(host,"promoter@demo.local");var general=(await SalesCatalog(c)).Offers.Single(x=>x.Name=="Entrada general");var r=await WhatsAppReserve(c,general);var input=Verified(r.TotalCents,"YAPE-123456");
        await Success<CommerceResult>(await Business(p,VerifyUrl(r.OrderId),input with{Approved=false,Note="Comprobante sin abono visible"}));var noTickets=await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{r.OrderId}");Assert.Empty(noTickets!.Tickets);Assert.Equal("PendingPayment",noTickets.Status);
        await Success<CommerceResult>(await Business(p,VerifyUrl(r.OrderId),input));var other=await WhatsAppReserve(c,general);Assert.Equal(HttpStatusCode.Conflict,(await Business(p,VerifyUrl(other.OrderId),input)).StatusCode);var d=await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{other.OrderId}");Assert.Empty(d!.Tickets);
    }
    [Fact] public async Task Expired_manual_payment_is_reviewed_without_taking_another_buyers_inventory()
    {
        using var host=ManualFactory();using var a=await LoginOn(host,"admin.a@demo.local");await ManualSetup(a);using var c=await LoginOn(host,"customer@demo.local");using var p=await LoginOn(host,"promoter@demo.local");var box=(await SalesCatalog(c)).Offers.Single(x=>x.Name.StartsWith("Box B3"));var r=await WhatsAppReserve(c,box);await ExpireHold(r.HoldId);var other=await WhatsAppReserve(a,box);
        var result=await Success<CommerceResult>(await Business(p,VerifyUrl(r.OrderId),Verified(r.TotalCents)));Assert.Equal("PaymentReview",result.Status);var d=await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{r.OrderId}");Assert.Empty(d!.Tickets);Assert.Null(d.Voucher);Assert.Contains("Succeeded",d.PaymentStatuses);var current=await a.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{other.OrderId}");Assert.Equal("PendingPayment",current!.Status);
    }
}
