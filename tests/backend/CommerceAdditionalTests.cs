using System.Net;
using System.Net.Http.Json;
using ClubPlatform.Api;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClubPlatform.Tests;
public partial class PhaseOneTests
{
    [Fact] public async Task F4_cashier_can_sell_only_assigned_venue_and_cannot_issue_courtesy()
    {
        using (var scope = factory.Services.CreateScope()) {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[] { "AdminA", "AdminB", "Security", "Bar", "Customer", "Cashier" }.ToDictionary(x => "Seed:Passwords:"+x, _ => (string?)password)).Build();
            await DemoSeed.Run(scope.ServiceProvider.GetRequiredService<AppDbContext>(),scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>(),config);
        }
        using var cashier = await Login("cashier@demo.local"); var general = (await SalesCatalog(cashier)).Offers.Single(x => x.Name == "Entrada general"); var request = new StaffSaleRequest("customer@demo.local", [new(general.Id,1)], "Test");
        var sold = await Success<CommerceResult>(await Business(cashier,A+$"/events/{SalesEvent}/cash-orders",request)); Assert.Equal("Confirmed",sold.Status);
        Assert.Equal(HttpStatusCode.Forbidden,(await Business(cashier,A+$"/events/{SalesEvent}/complimentary-orders",request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Business(cashier,A2+$"/events/{SalesEvent}/cash-orders",request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Business(cashier,B+$"/events/{SalesEvent}/cash-orders",request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Mutate(cashier,A+$"/events/{SalesEvent}/offers",HttpMethod.Post,new OfferSave(null,"Ataque",100,general.ResourceId,1,1,[]))).StatusCode);
    }
    [Fact] public async Task F3_background_worker_releases_persisted_expiration_without_customer_request()
    {
        using var c = await Login("customer@demo.local"); var general = (await SalesCatalog(c)).Offers.Single(x => x.Name == "Entrada general"); var hold = await Reserve(c,general,5); var order = await OrderFor(c,hold); await ExpireHold(hold.Id);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(25); var expired = false;
        while (DateTimeOffset.UtcNow < deadline) {
            await Task.Delay(500); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if ((await db.Set<Hold>().SingleAsync(x => x.Id == hold.Id)).Status != "Expired") continue;
            Assert.Equal("Expired",(await db.Set<Order>().SingleAsync(x => x.Id == order.Id)).Status);
            Assert.All(await db.Set<EventInventory>().Where(x => x.EventId == SalesEvent).ToListAsync(),x => Assert.Equal(0,x.Held));
            expired = true; break;
        }
        Assert.True(expired,"The persistent expiration worker did not release the hold.");
    }
}
