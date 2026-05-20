using Microsoft.EntityFrameworkCore;
using SmileyApi.Infrastructure.Data;

namespace SmileyApi.Api.Endpoints;

public static class WidgetEndpoints
{
    public static void MapWidgetEndpoints(this WebApplication app)
    {
        app.MapGet("/widget/score", async (
            string? navnelbnr,
            SmileyDbContext db,
            IConfiguration config) =>
        {
            if (!config.GetValue<bool>("Widget:AllowAnonymousEmbed"))
                return Results.Json(
                    new { error = new { code = "embed_disabled", message = "Anonymous embedding is disabled." } },
                    statusCode: 403);

            if (string.IsNullOrWhiteSpace(navnelbnr) || !int.TryParse(navnelbnr.Trim(), out var navnelbnrId))
                return Results.Json(
                    new { error = new { code = "missing_navnelbnr", message = "navnelbnr query parameter is required and must be a number." } },
                    statusCode: 400);

            var est = await db.Establishments
                .Where(e => e.Navnelbnr == navnelbnrId)
                .Select(e => new
                {
                    e.Navnelbnr,
                    e.LatestScore,
                    e.ReportUrl,
                    LastInspectedOn = e.Inspections
                        .OrderByDescending(i => i.InspectedOn)
                        .Select(i => (DateOnly?)i.InspectedOn)
                        .FirstOrDefault()
                    // History = e.Inspections
                    //     .OrderByDescending(i => i.InspectedOn)
                    //     .Take(5)
                    //     .Select(i => new { score = i.SmileyScore, date = i.InspectedOn })
                    //     .ToList()
                })
                .FirstOrDefaultAsync();

            if (est is null)
                return Results.Json(
                    new { error = new { code = "not_found", message = "No establishment found for this Navnelbnr." } },
                    statusCode: 404);

            return Results.Json(new
            {
                navnelbnr       = est.Navnelbnr,
                score           = est.LatestScore,
                reportUrl       = est.ReportUrl,
                lastInspectedOn = est.LastInspectedOn,
                // history      = est.History,
                tier            = "open"
            });
        })
        .RequireCors("widget")
        .RequireRateLimiting("widget-ip")
        .ExcludeFromDescription();
    }
}
