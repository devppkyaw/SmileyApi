using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;

namespace SmilrApi.Api.Endpoints;

public static class EstablishmentEndpoints
{
    public static void MapEstablishmentEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsProduction())
        {
            app.MapPost("/admin/keys", async (KeyRequest req, IApiKeyService svc, CancellationToken ct) =>
            {
                var (plaintext, _) = await svc.GenerateAsync(req.Email, req.Tier, ct);
                return Results.Ok(new { key = plaintext, email = req.Email, tier = req.Tier });
            });
        }

        var v1 = app.MapGroup("/v1/establishments")
                    .RequireRateLimiting("api-key-tier");

        v1.MapGet("/{cvr}", async (string cvr, IEstablishmentRepository repo, CancellationToken ct) =>
        {
            var e = await repo.GetByCvrAsync(cvr, ct);
            return e is null
                ? Error(404, "not_found", $"No establishment found for CVR '{cvr}'.")
                : Results.Ok(ToDto(e));
        });

        v1.MapGet("/search", async (
            string? q, int page, int limit,
            IEstablishmentRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Error(400, "bad_request", "Query parameter 'q' is required.");
            limit = Math.Clamp(limit == 0 ? 20 : limit, 1, 100);
            page  = Math.Max(page == 0 ? 1 : page, 1);
            var results = await repo.SearchAsync(q, page, limit, ct);
            return Results.Ok(new { page, limit, results = results.Select(ToSummaryDto) });
        });

        v1.MapGet("/nearby", async (
            double? lat, double? lng, double? radius,
            IEstablishmentRepository repo, CancellationToken ct) =>
        {
            if (lat is null || lng is null || radius is null)
                return Error(400, "bad_request", "Parameters 'lat', 'lng', and 'radius' are required.");
            if (radius <= 0 || radius > 50)
                return Error(400, "bad_request", "Parameter 'radius' must be between 0 and 50 km.");
            var results = await repo.GetNearbyAsync(lat.Value, lng.Value, radius.Value, ct);
            return Results.Ok(new { lat, lng, radiusKm = radius, results = results.Select(ToSummaryDto) });
        });

        v1.MapGet("/{cvr}/history", async (string cvr, IEstablishmentRepository repo, CancellationToken ct) =>
        {
            var history = await repo.GetHistoryAsync(cvr, ct);
            if (history.Count == 0)
                return Error(404, "not_found", $"No inspection history found for CVR '{cvr}'.");
            return Results.Ok(new
            {
                cvr,
                count = history.Count,
                inspections = history.Select(i => new InspectionDto(i.SmileyScore, i.InspectedOn))
            });
        });
    }

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);

    private static EstablishmentDto ToDto(Establishment e) => new(
        e.Navnelbnr, e.CvrNumber, e.Name, e.Address, e.PostalCode, e.City,
        e.IndustryCode, e.IndustryName, e.GeoLat, e.GeoLng, e.ReportUrl, e.LatestScore,
        e.Inspections.OrderByDescending(i => i.InspectedOn)
                     .Select(i => new InspectionDto(i.SmileyScore, i.InspectedOn))
                     .FirstOrDefault());

    private static EstablishmentSummaryDto ToSummaryDto(Establishment e) => new(
        e.Navnelbnr, e.CvrNumber, e.Name, e.Address, e.PostalCode, e.City, e.LatestScore);
}

public record KeyRequest(string Email, string Tier = "free");
public record InspectionDto(int Score, DateOnly InspectedOn);
public record EstablishmentDto(
    int Navnelbnr, string? CvrNumber, string Name, string? Address,
    string? PostalCode, string? City, string? IndustryCode, string? IndustryName,
    double? GeoLat, double? GeoLng, string? ReportUrl, int? LatestScore,
    InspectionDto? LatestInspection);
public record EstablishmentSummaryDto(
    int Navnelbnr, string? CvrNumber, string Name, string? Address,
    string? PostalCode, string? City, int? LatestScore);
