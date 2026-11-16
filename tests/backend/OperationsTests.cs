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
    private static string Ops(string suffix,string venue=A,Guid? eventId=null)=>venue+$"/events/{eventId??SalesEvent}/"+suffix;
    private async Task LiveWindows()
    {
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var e=await db.Events.SingleAsync(x=>x.Id==SalesEvent);var now=DateTimeOffset.UtcNow;
        e.StartsAt=now.AddMinutes(-30);e.EndsAt=now.AddHours(3);e.EntryOpensAt=now.AddHours(-1);e.EntryClosesAt=now.AddHours(2);e.RedemptionOpensAt=e.StartsAt;e.RedemptionClosesAt=now.AddHours(2.5);await db.SaveChangesAsync();
    }
    private async Task<OrderDto> OperationalOrder(HttpClient buyer,string box="Box B3")
    {
        var cat=await SalesCatalog(buyer);await LiveWindows();var h=await Reserve(buyer,cat.Offers.Single(x=>x.Name.StartsWith(box)));var order=await OrderFor(buyer,h);await Success<CommerceResult>(await Business(buyer,$"/api/v1/demo/orders/{order.Id}/payment-result",new DemoPaymentRequest(true)));return (await buyer.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{order.Id}"))!;
    }
    private async Task<HttpClient> SecondEmployee(string email,string role)
    {
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var users=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();var user=new AppUser{UserName=email,Email=email};Assert.True((await users.CreateAsync(user,password)).Succeeded);
        var tenant=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");db.Memberships.Add(new(){TenantId=tenant,UserId=user.Id,Role=role});db.Assignments.Add(new(){TenantId=tenant,VenueId=Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),UserId=user.Id});await db.SaveChangesAsync();return await Login(email);
    }
    [Fact] public async Task F5_inspect_is_non_consuming_and_two_security_employees_admit_only_once()
    {
        using var customer=await Login("customer@demo.local");var order=await OperationalOrder(customer);using var s=await Login("security@demo.local");using var s2=await SecondEmployee("security2@demo.local",StaffRoles.Security);var token=order.Tickets[0].Token;
        var inspection=await Success<ScanOutcome>(await Mutate(s,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(token)));Assert.Equal("VALID",inspection.Code);Assert.True(inspection.CanConfirm);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Empty(await db.Set<AdmissionLog>().ToListAsync());Assert.Equal("Issued",(await db.Set<Ticket>().SingleAsync(x=>x.Id==order.Tickets[0].Id)).Status);}
        var body=new AdmissionRequest(token,"Puerta 1");var race=await Task.WhenAll(Business(s,Ops("admissions/check-in"),body,"entry1"),Business(s2,Ops("admissions/check-in"),body,"entry2"));Assert.Single(race,x=>x.IsSuccessStatusCode);Assert.Single(race,x=>x.StatusCode==HttpStatusCode.Conflict);
        var winner=race[0].IsSuccessStatusCode?s:s2;var loser=race[0].IsSuccessStatusCode?s2:s;var key=race[0].IsSuccessStatusCode?"entry1":"entry2";
        var first=await Success<ScanOutcome>(race.Single(x=>x.IsSuccessStatusCode));var replay=await Success<ScanOutcome>(await Business(winner,Ops("admissions/check-in"),body,key));Assert.Equal(first.OperationId,replay.OperationId);
        var recovered=await winner.GetFromJsonAsync<ScanOutcome>(Ops("admissions/operations/"+key));Assert.Equal(first.OperationId,recovered!.OperationId);Assert.Equal(HttpStatusCode.NotFound,(await loser.GetAsync(Ops("admissions/operations/"+key))).StatusCode);
        var used=await Success<ScanOutcome>(await Mutate(s,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(token)));Assert.Equal("ALREADY_USED",used.Code);Assert.NotNull(used.RecordedAt);Assert.False(used.CanConfirm);
        using var check=factory.Services.CreateScope();var database=check.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(1,await database.Set<AdmissionLog>().CountAsync());Assert.Equal(1,await database.Set<AuditEntry>().CountAsync(x=>x.Action=="AdmissionRecorded"));Assert.DoesNotContain(token,System.Text.Json.JsonSerializer.Serialize(await database.Set<ScanAttempt>().ToListAsync()));
    }
    [Fact] public async Task F5_bar_requires_admission_and_delivers_only_selected_quantity_idempotently()
    {
        using var customer=await Login("customer@demo.local");var order=await OperationalOrder(customer);using var security=await Login("security@demo.local");using var bar=await Login("bar@demo.local");var token=order.Voucher!.Token;
        var before=await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(token)));Assert.Equal("ENTRY_REQUIRED",before.Code);Assert.False(before.CanConfirm);Assert.All(before.Lines!,x=>Assert.Equal(0,x.Delivered));
        var soda=before.Lines!.Single(x=>x.Name=="Gaseosa");Assert.Equal(HttpStatusCode.Conflict,(await Business(bar,Ops("redemptions"),new RedemptionRequest(token,[new(soda.Id,1)]))).StatusCode);
        await Success<ScanOutcome>(await Business(security,Ops("admissions/check-in"),new AdmissionRequest(order.Tickets[0].Token)));
        var ready=await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(token)));Assert.Equal("VALID",ready.Code);Assert.All(ready.Lines!,x=>Assert.Equal(0,x.Delivered));
        var body=new RedemptionRequest(token,[new(soda.Id,1)],"Barra principal");var first=await Success<ScanOutcome>(await Business(bar,Ops("redemptions"),body,"soda-one"));Assert.Equal("REDEEMED",first.Code);Assert.Equal("PartiallyRedeemed",first.State);Assert.Equal(1,first.Lines!.Single(x=>x.Name=="Gaseosa").Remaining);Assert.Equal(1,first.Lines!.Single(x=>x.Name=="Whisky").Remaining);
        var replay=await Success<ScanOutcome>(await Business(bar,Ops("redemptions"),body,"soda-one"));Assert.Equal(first.OperationId,replay.OperationId);Assert.Equal(HttpStatusCode.Conflict,(await Business(bar,Ops("redemptions"),body with{Lines=[new(soda.Id,2)]},"soda-one")).StatusCode);
        var recovered=await bar.GetFromJsonAsync<ScanOutcome>(Ops("redemptions/operations/soda-one"));Assert.Equal(first.OperationId,recovered!.OperationId);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Single(await db.Set<RedemptionOperation>().ToListAsync());Assert.Single(await db.Set<RedemptionLine>().ToListAsync());Assert.Equal("Barra principal",(await db.Set<RedemptionOperation>().SingleAsync()).Station);
    }
    [Fact] public async Task F5_two_bar_employees_cannot_redeem_the_last_bottle_twice()
    {
        using var customer=await Login("customer@demo.local");var order=await OperationalOrder(customer);using var security=await Login("security@demo.local");using var bar=await Login("bar@demo.local");using var bar2=await SecondEmployee("bar2@demo.local",StaffRoles.BarStaff);
        await Success<ScanOutcome>(await Business(security,Ops("admissions/check-in"),new AdmissionRequest(order.Tickets[0].Token)));var view=await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(order.Voucher!.Token)));var whisky=view.Lines!.Single(x=>x.Name=="Whisky");var soda=view.Lines!.Single(x=>x.Name=="Gaseosa");
        var body=new RedemptionRequest(order.Voucher.Token,[new(whisky.Id,1)]);var race=await Task.WhenAll(Business(bar,Ops("redemptions"),body),Business(bar2,Ops("redemptions"),body));Assert.Single(race,x=>x.IsSuccessStatusCode);Assert.Single(race,x=>x.StatusCode==HttpStatusCode.Conflict);
        await Success<ScanOutcome>(await Business(bar,Ops("redemptions"),new RedemptionRequest(order.Voucher.Token,[new(soda.Id,2)])));var empty=await Success<ScanOutcome>(await Mutate(bar2,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(order.Voucher.Token)));Assert.Equal("ALREADY_REDEEMED",empty.Code);Assert.Equal("Redeemed",empty.State);Assert.All(empty.Lines!,x=>Assert.Equal(0,x.Remaining));
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(1,(await db.Set<ConsumptionEntitlement>().SingleAsync(x=>x.Id==whisky.Id)).Delivered);Assert.Equal(2,await db.Set<RedemptionOperation>().CountAsync());
    }
    [Fact] public async Task F5_permissions_wrong_credential_type_and_cross_scope_replays_are_denied()
    {
        using var customer=await Login("customer@demo.local");var order=await OperationalOrder(customer);using var security=await Login("security@demo.local");using var bar=await Login("bar@demo.local");using var admin=await Login("admin.a@demo.local");using var b=await Login("admin.b@demo.local");
        Assert.Equal(HttpStatusCode.NotFound,(await Mutate(customer,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(order.Tickets[0].Token))).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await Mutate(b,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(order.Tickets[0].Token))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Mutate(bar,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(order.Tickets[0].Token))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await Mutate(security,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(order.Voucher!.Token))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await Mutate(admin,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(order.Tickets[0].Token))).StatusCode);
        Assert.Equal("WRONG_CREDENTIAL_TYPE",(await Success<ScanOutcome>(await Mutate(security,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(order.Voucher!.Token)))).Code);Assert.Equal("WRONG_CREDENTIAL_TYPE",(await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(order.Tickets[0].Token)))).Code);
        Assert.Equal(HttpStatusCode.Forbidden,(await Mutate(security,Ops("admissions/inspect",A2),HttpMethod.Post,new TokenRequest(order.Tickets[0].Token))).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await security.PostAsJsonAsync(Ops("admissions/check-in"),new AdmissionRequest(order.Tickets[0].Token))).StatusCode);
        var body=new AdmissionRequest(order.Tickets[0].Token);await Success<ScanOutcome>(await Business(security,Ops("admissions/check-in"),body,"scoped-entry"));
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var uid=(await db.Users.SingleAsync(x=>x.Email=="security@demo.local")).Id;db.Assignments.Add(new(){TenantId=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),VenueId=Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),UserId=uid});await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.NotFound,(await Business(security,Ops("admissions/check-in",A2),body,"scoped-entry")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await security.GetAsync(Ops("admissions/operations/scoped-entry",A2))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Business(bar,A+$"/events/{SalesEvent}/complimentary-orders",new StaffSaleRequest("customer@demo.local",[],"Ataque"))).StatusCode);
    }
    [Fact] public async Task F5_rechecks_windows_revocation_and_invalid_codes_at_confirmation()
    {
        using var c=await Login("customer@demo.local");var order=await OperationalOrder(c);using var security=await Login("security@demo.local");using var bar=await Login("bar@demo.local");
        Assert.Equal("INVALID",(await Success<ScanOutcome>(await Mutate(security,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest("t_"+new string('A',64))))).Code);
        Assert.Equal("VALID",(await Success<ScanOutcome>(await Mutate(security,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(order.Tickets[0].Token)))).Code);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();await db.Set<Ticket>().Where(x=>x.Id==order.Tickets[0].Id).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.Status,"Revoked"));}
        Assert.Equal(HttpStatusCode.Conflict,(await Business(security,Ops("admissions/check-in"),new AdmissionRequest(order.Tickets[0].Token))).StatusCode);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var e=await db.Events.SingleAsync(x=>x.Id==SalesEvent);e.EntryClosesAt=DateTimeOffset.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();}
        Assert.Equal("OUTSIDE_WINDOW",(await Success<ScanOutcome>(await Mutate(security,Ops("admissions/inspect"),HttpMethod.Post,new TokenRequest(order.Tickets[1].Token)))).Code);Assert.Equal(HttpStatusCode.Conflict,(await Business(security,Ops("admissions/check-in"),new AdmissionRequest(order.Tickets[1].Token))).StatusCode);
        await LiveWindows();await Success<ScanOutcome>(await Business(security,Ops("admissions/check-in"),new AdmissionRequest(order.Tickets[1].Token)));
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var e=await db.Events.SingleAsync(x=>x.Id==SalesEvent);e.RedemptionClosesAt=DateTimeOffset.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();}
        Assert.Equal("OUTSIDE_WINDOW",(await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(order.Voucher!.Token)))).Code);
        await LiveWindows();using(var scope=factory.Services.CreateScope()){await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ConsumptionVoucher>().Where(x=>x.OrderId==order.Id).ExecuteUpdateAsync(x=>x.SetProperty(v=>v.Status,"Revoked"));}
        Assert.Equal("REVOKED",(await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(order.Voucher!.Token)))).Code);
    }
    [Fact] public async Task F5_cross_order_lines_invalid_quantities_and_relational_cross_voucher_writes_fail_atomically()
    {
        using var c=await Login("customer@demo.local");var order=await OperationalOrder(c);var other=await OperationalOrder(c,"Box B4");using var s=await Login("security@demo.local");using var bar=await Login("bar@demo.local");await Success<ScanOutcome>(await Business(s,Ops("admissions/check-in"),new AdmissionRequest(order.Tickets[0].Token)));
        var first=await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(order.Voucher!.Token)));var second=await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(other.Voucher!.Token)));var line=first.Lines![0];var foreign=second.Lines![0];
        Assert.Equal(HttpStatusCode.Conflict,(await Business(bar,Ops("redemptions"),new RedemptionRequest(order.Voucher.Token,[new(line.Id,1),new(foreign.Id,1)]))).StatusCode);
        foreach(var quantity in new[]{0,-1,1001})Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Business(bar,Ops("redemptions"),new RedemptionRequest(order.Voucher.Token,[new(line.Id,quantity)]))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Business(bar,Ops("redemptions"),new RedemptionRequest(order.Voucher.Token,[new(line.Id,1),new(line.Id,1)]))).StatusCode);
        var untouched=await Success<ScanOutcome>(await Mutate(bar,Ops("redemptions/inspect"),HttpMethod.Post,new TokenRequest(order.Voucher.Token)));Assert.All(untouched.Lines!,x=>Assert.Equal(0,x.Delivered));
        var success=await Success<ScanOutcome>(await Business(bar,Ops("redemptions"),new RedemptionRequest(order.Voucher.Token,[new(line.Id,1)])));
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var op=await db.Set<RedemptionOperation>().SingleAsync(x=>x.Id==success.OperationId);db.Add(new RedemptionLine{TenantId=op.TenantId,VenueId=op.VenueId,EventId=op.EventId,VoucherId=op.VoucherId,OperationId=op.Id,EntitlementId=foreign.Id,Quantity=1});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }
    [Fact] public async Task F5_a_ticket_from_another_event_is_invalid_even_in_the_same_venue()
    {
        using var c=await Login("customer@demo.local");var order=await OperationalOrder(c);using var a=await Login("admin.a@demo.local");using var s=await Login("security@demo.local");var version=await PublishedMap(a);var draft=await Success<EventResponse>(await Mutate(a,A+"/events",HttpMethod.Post,Night(version.Id)));await Success<EventResponse>(await Mutate(a,A+$"/events/{draft.Id}/publish",HttpMethod.Post,new RevisionRequest(1)));
        var result=await Success<ScanOutcome>(await Mutate(s,Ops("admissions/inspect",eventId:draft.Id),HttpMethod.Post,new TokenRequest(order.Tickets[0].Token)));Assert.Equal("INVALID",result.Code);Assert.Null(result.SubjectId);Assert.Null(result.Zone);Assert.Null(result.RecordedAt);
        Assert.Equal(HttpStatusCode.Conflict,(await Business(s,Ops("admissions/check-in",eventId:draft.Id),new AdmissionRequest(order.Tickets[0].Token))).StatusCode);
    }
}
