using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using Xunit;
namespace ClubPlatform.Tests;

public class ConfigurationTests
{
    [Theory]
    [InlineData("Payments:Provider", "Demo")]
    [InlineData("Seed:Enabled", "true")]
    public void Production_refuses_demo_configuration(string key, string value)
    { using var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }))); var e = Assert.ThrowsAny<Exception>(() => f.CreateClient()); Assert.Contains("forbidden", e.ToString()); }
    [Fact]
    public async Task Database_outage_does_not_report_ready_or_leak_connection_details()
    { using var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=1;Database=missing;Username=secret-user;Password=secret-password;Timeout=1", ["DataProtection:Path"] = Path.Combine(Path.GetTempPath(), "clubplatform-test-keys"), ["Seed:Enabled"] = "false" }))); using var c = f.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/live")).StatusCode); var ready = await c.GetAsync("/health/ready"); Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode); Assert.DoesNotContain("secret", await ready.Content.ReadAsStringAsync()); }
}
