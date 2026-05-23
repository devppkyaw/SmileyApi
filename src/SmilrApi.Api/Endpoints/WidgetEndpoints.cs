using Microsoft.EntityFrameworkCore;
using SmilrApi.Infrastructure.Data;

namespace SmilrApi.Api.Endpoints;

public static class WidgetEndpoints
{
    public static void MapWidgetEndpoints(this WebApplication app)
    {
        app.MapGet("/widget/score", async (
            string? navnelbnr,
            string? businessId,
            SmilrDbContext db,
            IConfiguration config) =>
        {
            // Resolve tier from businessId if provided; otherwise anonymous free tier
            string tier = "free";
            if (!string.IsNullOrWhiteSpace(businessId))
            {
                var business = await db.Businesses
                    .Where(b => b.BusinessId == businessId && b.IsEmailVerified)
                    .Select(b => b.Tier)
                    .FirstOrDefaultAsync();
                if (business is not null)
                    tier = business;
            }
            else if (!config.GetValue<bool>("Widget:AllowAnonymousEmbed"))
            {
                return Results.Json(
                    new { error = new { code = "embed_disabled", message = "Anonymous embedding is disabled." } },
                    statusCode: 403);
            }

            if (string.IsNullOrWhiteSpace(navnelbnr) || !int.TryParse(navnelbnr.Trim(), out var navnelbnrId))
                return Results.Json(
                    new { error = new { code = "missing_navnelbnr", message = "navnelbnr query parameter is required and must be a number." } },
                    statusCode: 400);

            var est = await db.Establishments
                .Where(e => e.Navnelbnr == navnelbnrId)
                .Select(e => new
                {
                    e.Navnelbnr,
                    e.CvrNumber,
                    e.Name,
                    e.LatestScore,
                    e.LatestScoreDate,
                    e.ReportUrl,
                    e.VirksomhedsType,
                    e.Pixibranche
                })
                .FirstOrDefaultAsync();

            if (est is null)
                return Results.Json(
                    new { error = new { code = "not_found", message = "No establishment found for this Navnelbnr." } },
                    statusCode: 404);

            return Results.Json(new
            {
                navnelbnr   = est.Navnelbnr,
                cvr         = est.CvrNumber,
                name        = est.Name,
                score       = est.LatestScore,
                scoreDate   = est.LatestScoreDate,
                reportUrl   = est.ReportUrl,
                displayMode = est.VirksomhedsType == "Detail" ? "smiley" : "document",
                pixibranche = est.Pixibranche,
                tier
            });
        })
        .RequireCors("widget")
        .RequireRateLimiting("widget-ip")
        .ExcludeFromDescription();
    }
}
