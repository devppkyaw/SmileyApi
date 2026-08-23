using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmilrApi.Infrastructure.Services;

namespace SmilrApi.Api.Tests;

public class AcsEmailServiceRecipientResolutionTests
{
    // AcsEmailService's constructor requires a syntactically-valid ACS connection string, but
    // ResolveRecipient never touches the EmailClient it builds from it — a dummy value is enough.
    private const string DummyConnectionString = "endpoint=https://example.communication.azure.com/;accesskey=dGVzdA==";

    private static AcsEmailService CreateService(string? overrideAddress, string? systemMonitorAddress)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Acs:ConnectionString"] = DummyConnectionString,
                ["Email:OverrideAddress"] = overrideAddress,
                ["Email:SystemMonitorAddress"] = systemMonitorAddress
            })
            .Build();

        return new AcsEmailService(config, NullLogger<AcsEmailService>.Instance);
    }

    [Fact]
    public void No_override_no_monitor_sends_to_real_recipient_with_no_cc()
    {
        var service = CreateService(overrideAddress: null, systemMonitorAddress: null);

        var plan = service.ResolveRecipient("user@example.com");

        Assert.Equal("user@example.com", plan.PrimaryRecipient);
        Assert.Equal(string.Empty, plan.Banner);
        Assert.Empty(plan.CcAddresses);
    }

    [Fact]
    public void No_override_with_monitor_ccs_the_monitor_address()
    {
        var service = CreateService(overrideAddress: null, systemMonitorAddress: "system@smilrhq.dk");

        var plan = service.ResolveRecipient("user@example.com");

        Assert.Equal("user@example.com", plan.PrimaryRecipient);
        Assert.Equal(string.Empty, plan.Banner);
        Assert.Equal(new[] { "system@smilrhq.dk" }, plan.CcAddresses);
    }

    [Fact]
    public void No_override_monitor_equal_to_recipient_does_not_duplicate_cc()
    {
        var service = CreateService(overrideAddress: null, systemMonitorAddress: "SAME@smilrhq.dk");

        var plan = service.ResolveRecipient("same@smilrhq.dk");

        Assert.Equal("same@smilrhq.dk", plan.PrimaryRecipient);
        Assert.Empty(plan.CcAddresses);
    }

    [Fact]
    public void Override_set_fully_redirects_with_banner_and_no_cc_regardless_of_monitor()
    {
        var service = CreateService(overrideAddress: "system@smilrhq.dk", systemMonitorAddress: "system@smilrhq.dk");

        var plan = service.ResolveRecipient("user@example.com");

        Assert.Equal("system@smilrhq.dk", plan.PrimaryRecipient);
        Assert.Contains("user@example.com", plan.Banner);
        Assert.Empty(plan.CcAddresses);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Override_null_empty_or_whitespace_is_treated_as_not_set(string? overrideAddress)
    {
        var service = CreateService(overrideAddress, systemMonitorAddress: "system@smilrhq.dk");

        var plan = service.ResolveRecipient("user@example.com");

        Assert.Equal("user@example.com", plan.PrimaryRecipient);
        Assert.Equal(string.Empty, plan.Banner);
        Assert.Equal(new[] { "system@smilrhq.dk" }, plan.CcAddresses);
    }
}
