using System.Net;
using System.Net.Http.Json;
using ClubPlatform.Api;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClubPlatform.Tests;
public partial class PhaseOneTests
{
    private static readonly Guid SalesEvent = Guid.Parse("aaaaaaaa-3333-0000-0000-000000000001");
    private async Task<CatalogDto> SalesCatalog(HttpClient c) { using var scope = factory.Services.CreateScope(); await CommerceSeed.Run(scope.ServiceProvider.GetRequiredService<AppDbContext>()); return (await c.GetFromJsonAsync<CatalogDto>($"/api/v1/public/events/{SalesEvent}/catalog"))!; }
    private async Task<HttpResponseMessage> Business(HttpClient c, string url, object body, string? key = null) { var csrf = await c.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf"); var req = new HttpRequestMessage(HttpMethod.Post,url) { Content = JsonContent.Create(body) }; req.Headers.Add("X-CSRF-TOKEN",csrf!.Token); req.Headers.Add("Idempotency-Key",key ?? Guid.NewGuid().ToString()); return await c.SendAsync(req); }
    private HoldRequest Select(OfferDto offer, int quantity = 1) => new(SalesEvent, [new(offer.Id,quantity)]);
    private async Task<CommerceResult> Reserve(HttpClient c, OfferDto o, int q = 1) => await Success<CommerceResult>(await Business(c,"/api/v1/holds",Select(o,q)));
    private async Task<CommerceResult> OrderFor(HttpClient c, CommerceResult h) => await Success<CommerceResult>(await Business(c,"/api/v1/orders",new OrderRequest(h.Id)));
    private async Task ExpireHold(Guid id) { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Hold>().Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(h => h.ExpiresAt,DateTimeOffset.UtcNow.AddMinutes(-1))); }
    [Fact] public async Task F3_exclusive_concurrency_and_idempotency_are_atomic()
    {
        using var c = await Login("customer@demo.local"); using var other = await Login("admin.a@demo.local"); var cat = await SalesCatalog(c); var box = cat.Offers.Single(x => x.Name.StartsWith("Box B3"));
        var results = await Task.WhenAll(Business(c,"/api/v1/holds",Select(box),"box-race"),Business(other,"/api/v1/holds",Select(box),"box-race-other"));
        Assert.Single(results,x => x.IsSuccessStatusCode); Assert.Single(results,x => x.StatusCode == HttpStatusCode.Conflict);
        var winner = results[0].IsSuccessStatusCode ? c : other; var key = results[0].IsSuccessStatusCode ? "box-race" : "box-race-other";
        var original = await Success<CommerceResult>(results.Single(x => x.IsSuccessStatusCode)); var replay = await Success<CommerceResult>(await Business(winner,"/api/v1/holds",Select(box),key)); Assert.Equal(original.Id,replay.Id);
        Assert.Equal(HttpStatusCode.Conflict,(await Business(winner,"/api/v1/holds",Select(box,2),key)).StatusCode);
        cat = (await c.GetFromJsonAsync<CatalogDto>($"/api/v1/public/events/{SalesEvent}/catalog"))!; Assert.Equal(10,cat.Inventory.Single(x => x.ResourceId == null).Held); Assert.Equal(0,cat.Inventory.Single(x => x.Name == "General").Held);
    }
    [Fact] public async Task F3_general_and_global_capacity_do_not_oversell_and_expire_without_worker()
    {
        using var c = await Login("customer@demo.local"); var cat = await SalesCatalog(c); var general = cat.Offers.Single(x => x.Name == "Entrada general");
        var results = await Task.WhenAll(Business(c,"/api/v1/holds",Select(general,50)),Business(c,"/api/v1/holds",Select(general,50))); Assert.Single(results,x => x.IsSuccessStatusCode); Assert.Single(results,x => x.StatusCode == HttpStatusCode.Conflict);
        var hold = await Success<CommerceResult>(results.Single(x => x.IsSuccessStatusCode)); await ExpireHold(hold.Id); var next = await Reserve(c,general,80); Assert.Equal("Active",next.Status);
        Assert.Equal(HttpStatusCode.Conflict,(await Business(c,"/api/v1/holds",Select(general))).StatusCode);
        // Artificially lower the global limit to demonstrate that zone stock alone never authorizes a sale.
        await Business(c,$"/api/v1/me/holds/{next.Id}/cancel",new {});
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Set<EventInventory>().Where(x => x.EventId == SalesEvent && x.ResourceId == null).ExecuteUpdateAsync(x => x.SetProperty(i => i.Limit,15)); }
        var box = cat.Offers.Single(x => x.Name.StartsWith("Box B3")); var race = await Task.WhenAll(Business(c,"/api/v1/holds",Select(box)), Business(c,"/api/v1/holds",Select(general,10))); Assert.Single(race,x => x.IsSuccessStatusCode); Assert.Single(race,x => x.StatusCode == HttpStatusCode.Conflict);
    }
    [Fact] public async Task F4_box_payment_emits_once_with_recoverable_encrypted_tokens_and_snapshot()
    {
        using var c = await Login("customer@demo.local"); var cat = await SalesCatalog(c); var box = cat.Offers.Single(x => x.Name.StartsWith("Box B3")); var h = await Reserve(c,box); var order = await OrderFor(c,h);
        var declined = await Success<CommerceResult>(await Business(c,$"/api/v1/demo/orders/{order.Id}/payment-result",new DemoPaymentRequest(false))); Assert.Equal("PendingPayment",declined.Status);
        var empty = await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{order.Id}"); Assert.Empty(empty!.Tickets);
        var paid = await Task.WhenAll(Business(c,$"/api/v1/demo/orders/{order.Id}/payment-result",new DemoPaymentRequest(true),"pay1"),Business(c,$"/api/v1/demo/orders/{order.Id}/payment-result",new DemoPaymentRequest(true),"pay2")); Assert.All(paid,x => Assert.True(x.IsSuccessStatusCode));
        var detail = await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{order.Id}"); Assert.Equal("Confirmed",detail!.Status); Assert.Equal(100000,detail.TotalCents); Assert.Equal(10,detail.Tickets.Count); Assert.Equal(10,detail.Tickets.Select(x => x.Token).Distinct().Count()); Assert.All(detail.Tickets,x => Assert.StartsWith("t_",x.Token)); Assert.StartsWith("v_",detail.Voucher!.Token); Assert.Equal(3,detail.Voucher.Lines.Sum(x => x.Granted)); Assert.Equal(2,detail.Voucher.Lines.Count);
        using var admin = await Login("admin.a@demo.local"); await Success<OfferDto>(await Mutate(admin,A+$"/events/{SalesEvent}/offers",HttpMethod.Post,new OfferSave(box.OfferId,"Precio nuevo",200000,box.ResourceId,1,10,box.Drinks.Select(x => new DrinkInput(x.ProductId,x.Quantity)).ToList(),1)));
        using var freshScope = factory.Services.CreateScope(); var service = freshScope.ServiceProvider.GetRequiredService<CommerceService>(); var userId = (await freshScope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(x => x.Email == "customer@demo.local")).Id; var restored = await service.OrderDetail(order.Id,userId); Assert.Equal(detail.Tickets.Select(x => x.Token),restored.Tickets.Select(x => x.Token)); Assert.Equal(100000,restored.Lines.Single().UnitPriceCents);
        var inventory = (await c.GetFromJsonAsync<CatalogDto>($"/api/v1/public/events/{SalesEvent}/catalog"))!.Inventory; Assert.Equal(10,inventory.Single(x => x.ResourceId == null).Sold); Assert.Equal(0,inventory.Single(x => x.Name == "General").Sold);
    }
    [Fact] public async Task F4_late_payment_is_review_without_benefits_or_inventory_theft()
    {
        using var c = await Login("customer@demo.local"); using var a = await Login("admin.a@demo.local"); var box = (await SalesCatalog(c)).Offers.Single(x => x.Name.StartsWith("Box B3")); var h = await Reserve(c,box); var o = await OrderFor(c,h); await ExpireHold(h.Id); await Reserve(a,box);
        var result = await Success<CommerceResult>(await Business(c,$"/api/v1/demo/orders/{o.Id}/payment-result",new DemoPaymentRequest(true))); Assert.Equal("PaymentReview",result.Status);
        var detail = await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{o.Id}"); Assert.Empty(detail!.Tickets); Assert.Null(detail.Voucher); Assert.Contains("Succeeded",detail.PaymentStatuses);
    }
    [Fact] public async Task F3_F4_permissions_csrf_and_complimentary_sales_use_same_capacity()
    {
        using var customer = await Login("customer@demo.local"); using var a = await Login("admin.a@demo.local"); using var b = await Login("admin.b@demo.local"); using var security = await Login("security@demo.local"); var cat = await SalesCatalog(a); var general = cat.Offers.Single(x => x.Name == "Entrada general");
        var sale = new StaffSaleRequest("customer@demo.local",[new(general.Id,3)],"Invitados de la organización"); var url = A+$"/events/{SalesEvent}/complimentary-orders";
        Assert.Equal(HttpStatusCode.NotFound,(await Business(b,url,sale)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden,(await Business(security,url,sale)).StatusCode); Assert.Equal(HttpStatusCode.NotFound,(await Business(customer,url,sale)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await customer.PostAsJsonAsync("/api/v1/holds",Select(general))).StatusCode);
        var complimentary = await Success<CommerceResult>(await Business(a,url,sale,"free")); var again = await Success<CommerceResult>(await Business(a,url,sale,"free")); Assert.Equal(complimentary.Id,again.Id); Assert.Equal(0,complimentary.TotalCents);
        var d = await customer.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{complimentary.Id}"); Assert.Equal(3,d!.Tickets.Count); Assert.Equal("Complimentary",d.Source);
        Assert.Equal(HttpStatusCode.NotFound,(await a.GetAsync($"/api/v1/me/orders/{complimentary.Id}")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.True(await db.Set<AuditEntry>().AnyAsync(x => x.SubjectId == complimentary.Id && x.Action == "ComplimentaryCreated" && x.Reason == sale.Reason));
        var cash = await Success<CommerceResult>(await Business(a,A+$"/events/{SalesEvent}/cash-orders",sale)); Assert.Equal("Confirmed",cash.Status);
        var hold = await Reserve(customer,general); var order = await OrderFor(customer,hold); Assert.Equal(HttpStatusCode.NotFound,(await Business(a,$"/api/v1/demo/orders/{order.Id}/payment-result",new DemoPaymentRequest(true))).StatusCode);
    }
    [Fact] public async Task F3_blocks_archive_and_cross_event_offer_cannot_be_bypassed()
    {
        using var a = await Login("admin.a@demo.local"); using var c = await Login("customer@demo.local"); var cat = await SalesCatalog(c); var box = cat.Offers.Single(x => x.Name.StartsWith("Box B3")); var inv = cat.Inventory.Single(x => x.ResourceId == box.ResourceId);
        await Success<System.Text.Json.JsonElement>(await Mutate(a,A+$"/events/{SalesEvent}/inventory/{inv.Id}/block",HttpMethod.Post,new BlockRequest(true))); Assert.Equal(HttpStatusCode.Conflict,(await Business(c,"/api/v1/holds",Select(box))).StatusCode);
        await Mutate(a,A+$"/events/{SalesEvent}/inventory/{inv.Id}/block",HttpMethod.Post,new BlockRequest(false)); var h = await Reserve(c,box); await Success<CommerceResult>(await Business(c,$"/api/v1/me/holds/{h.Id}/cancel",new {}));
        await Mutate(a,A+$"/events/{SalesEvent}/offers/{box.OfferId}/archive",HttpMethod.Post,new {}); Assert.Equal(HttpStatusCode.Conflict,(await Business(c,"/api/v1/holds",Select(box))).StatusCode);
    }
}
