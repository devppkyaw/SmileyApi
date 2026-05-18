using Microsoft.EntityFrameworkCore;
using SmileyApi.Infrastructure.Data;

namespace SmileyApi.Api.Endpoints;

public static class WidgetEndpoints
{
    public static void MapWidgetEndpoints(this WebApplication app)
    {
        app.MapGet("/widget/score", async (
            string? cvr,
            SmileyDbContext db,
            IConfiguration config) =>
        {
            if (!config.GetValue<bool>("Widget:AllowAnonymousEmbed"))
                return Results.Json(
                    new { error = new { code = "embed_disabled", message = "Anonymous embedding is disabled." } },
                    statusCode: 403);

            if (string.IsNullOrWhiteSpace(cvr))
                return Results.Json(
                    new { error = new { code = "missing_cvr", message = "cvr query parameter is required." } },
                    statusCode: 400);

            var est = await db.Establishments
                .Where(e => e.CvrNumber == cvr.Trim())
                .Select(e => new
                {
                    e.CvrNumber,
                    e.LatestScore,
                    e.ReportUrl,
                    LastInspectedOn = e.Inspections
                        .OrderByDescending(i => i.InspectedOn)
                        .Select(i => (DateOnly?)i.InspectedOn)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (est is null)
                return Results.Json(
                    new { error = new { code = "not_found", message = "No establishment found for this CVR." } },
                    statusCode: 404);

            return Results.Json(new
            {
                cvr             = est.CvrNumber,
                score           = est.LatestScore,
                reportUrl       = est.ReportUrl,
                lastInspectedOn = est.LastInspectedOn,
                tier            = "open"
            });
        })
        .RequireCors("widget")
        .RequireRateLimiting("widget-ip")
        .ExcludeFromDescription();
    }
}
