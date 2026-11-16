using System.Net.Http.Json;
using ClubPlatform.Api;
using ClubPlatform.Core;
using ClubPlatform.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClubPlatform.Tests;
public partial class PhaseOneTests
{
    [Fact] public async Task Reservation_codes_are_unique_across_connections_and_reject_duplicate_database_values()
    {
        using var c=await Login("customer@demo.local");var general=(await SalesCatalog(c)).Offers.Single(x=>x.Name=="Entrada general");var first=await OrderFor(c,await Reserve(c,general));var second=await OrderFor(c,await Reserve(c,general));
        Assert.Matches("^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$",first.Reference!);Assert.NotEqual(first.Reference,second.Reference);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var cs=db.Database.GetConnectionString()!;
        async Task<List<string>> Generate(){await using var connection=new Npgsql.NpgsqlConnection(cs);await connection.OpenAsync();await using var command=new Npgsql.NpgsqlCommand("SELECT app.next_order_reference() FROM generate_series(1,256)",connection);await using var reader=await command.ExecuteReaderAsync();var result=new List<string>();while(await reader.ReadAsync())result.Add(reader.GetString(0));return result;}
        var generated=(await Task.WhenAll(Generate(),Generate())).SelectMany(x=>x).ToList();Assert.Equal(512,generated.Distinct().Count());Assert.All(generated,x=>Assert.Matches("^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$",x));
        var duplicate=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app.\"Order\" SET \"Reference\"={first.Reference} WHERE \"Id\"={second.Id}"));Assert.Equal("23505",duplicate.SqlState);
        var detail=await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{first.Id}");Assert.Equal(first.Reference,detail!.Reference);var history=await c.GetFromJsonAsync<List<CommerceResult>>("/api/v1/me/orders");Assert.Equal(first.Reference,history!.Single(x=>x.Id==first.Id).Reference);
    }
    [Fact] public async Task Reservation_code_backfill_preserves_existing_orders_tokens_and_idempotent_whatsapp_links()
    {
        using var host=ManualFactory();using var admin=await LoginOn(host,"admin.a@demo.local");await ManualSetup(admin);using var c=await LoginOn(host,"customer@demo.local");var box=(await SalesCatalog(c)).Offers.Single(x=>x.Name.StartsWith("Box B3"));var reservation=await WhatsAppReserve(c,box,"legacy-reservation");await Success<CommerceResult>(await Business(admin,VerifyUrl(reservation.OrderId),Verified(reservation.TotalCents)));var before=(await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{reservation.OrderId}"))!;
        using(var scope=factory.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.StartsWith("clubtest_",db.Database.GetDbConnection().Database);var migrator=db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261116112009_AdmissionsAndRedemptions");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app.\"IdempotencyRecord\" SET \"ResultJson\"=jsonb_set(jsonb_set(\"ResultJson\", '{{payment,reference}}',to_jsonb({reservation.OrderId.ToString()}::text)), '{{payment,whatsAppUrl}}',to_jsonb(replace(\"ResultJson\" #>> '{{payment,whatsAppUrl}}',{before.Reference},{reservation.OrderId.ToString()}))) WHERE \"Operation\"='whatsapp-reservation' AND \"Key\"='legacy-reservation'");
            await migrator.MigrateAsync();
        }
        var after=(await c.GetFromJsonAsync<OrderDto>($"/api/v1/me/orders/{reservation.OrderId}"))!;Assert.Equal(before.Id,after.Id);Assert.Equal(before.TotalCents,after.TotalCents);Assert.Equal(before.Status,after.Status);Assert.Equal(before.Tickets.Select(t=>t.Token),after.Tickets.Select(t=>t.Token));Assert.Equal(before.Voucher!.Token,after.Voucher!.Token);Assert.Matches("^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$",after.Reference);
        var replay=await WhatsAppReserve(c,box,"legacy-reservation");Assert.Equal(after.Reference,replay.Payment.Reference);Assert.Equal(after.Id,replay.OrderId);var message=Uri.UnescapeDataString(replay.Payment.WhatsAppUrl!);Assert.Contains(after.Reference,message);Assert.DoesNotContain(after.Id.ToString(),message);
        var queue=await admin.GetFromJsonAsync<List<PaymentQueueItem>>(A+$"/events/{SalesEvent}/manual-payments");Assert.Equal(after.Reference,queue!.Single().Reference);
    }
}
