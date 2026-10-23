using Microsoft.AspNetCore.Http;
using SmilrApi.Api.Telemetry;

namespace SmilrApi.Api.Tests;

public class TelemetryFiltersTests
{
    [Theory]
    [InlineData("/health", true)]
    [InlineData("/HEALTH", true)]
    [InlineData("/healthz", false)]
    [InlineData("/find/aarhus", false)]
    [InlineData("/", false)]
    public void Only_the_health_probe_path_is_excluded(string path, bool expected) =>
        Assert.Equal(expected, TelemetryFilters.IsHealthProbe(new PathString(path)));
}
