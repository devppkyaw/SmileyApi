namespace SmilrApi.Api.Telemetry;

// Predicates for what NOT to send to Application Insights.
internal static class TelemetryFilters
{
    // Container Apps' readiness + liveness probes hit /health every few seconds — over half of all
    // AppRequests rows in prod, and none of them carry any signal.
    internal static bool IsHealthProbe(PathString path) =>
        path.Equals("/health", StringComparison.OrdinalIgnoreCase);
}
