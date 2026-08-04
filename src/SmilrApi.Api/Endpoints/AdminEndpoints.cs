using Microsoft.EntityFrameworkCore;
using SmilrApi.Api.Workers;
using SmilrApi.Core.Interfaces;
using SmilrApi.Infrastructure.Data;
using SmilrApi.Infrastructure.Services;

namespace SmilrApi.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        // Available in all environments — protected by Admin:Key header (skipped when key is empty/dev)
        app.MapPost("/admin/sync", async (
            HttpContext ctx,
            IConfiguration cfg,
            FodevareXmlParser parser,
            IServiceScopeFactory scopeFactory,
            CancellationToken ct) =>
        {
            var adminKey = cfg["Admin:Key"];
            if (!string.IsNullOrEmpty(adminKey))
            {
                var provided = ctx.Request.Headers["X-Admin-Key"].FirstOrDefault();
                if (provided != adminKey)
                    return Results.Json(Error("unauthorized", "Invalid admin key."), statusCode: 401);
            }

            if (!await XmlSyncWorker.Lock.WaitAsync(0, ct))
                return Results.Json(new { status = "already_running" }, statusCode: 409);

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var rows = await parser.ParseAsync(CancellationToken.None);
                    var syncRows = rows.Select(r => new SyncRow(
                        r.Navnelbnr, r.CvrNumber, r.Name, r.Address, r.PostalCode,
                        r.City, r.IndustryCode, r.IndustryName, r.GeoLat, r.GeoLng,
                        r.ReportUrl, r.VirksomhedsType, r.Pixibranche, r.LatestScoreDate, r.PNumber,
                        r.Inspections)).ToList();
                    var syncService = scope.ServiceProvider.GetRequiredService<EstablishmentSyncService>();
                    var scoreChanges = await syncService.SyncAsync(syncRows, CancellationToken.None);
                    if (scoreChanges.Count > 0)
                    {
                        var webhookSvc = scope.ServiceProvider.GetRequiredService<WebhookService>();
                        await webhookSvc.EnqueueDeliveriesAsync(
                            scoreChanges.Select(c => new WebhookScoreChange(c.EstablishmentId, c.OldScore, c.NewScore)).ToList(),
                            CancellationToken.None);
                    }
                }
                finally { XmlSyncWorker.Lock.Release(); }
            });

            return Results.Json(new { status = "started" }, statusCode: 202);
        });

        app.MapPost("/admin/verify", (HttpContext ctx, IConfiguration cfg) =>
        {
            var adminKey = cfg["Admin:Key"];
            if (string.IsNullOrEmpty(adminKey)) return Results.Ok();
            var provided = ctx.Request.Headers["X-Admin-Key"].FirstOrDefault();
            return provided == adminKey ? Results.Ok() : Results.Unauthorized();
        });

        app.MapGet("/admin/requests", async (HttpContext ctx, IConfiguration cfg, SmilrDbContext db, CancellationToken ct) =>
        {
            var adminKey = cfg["Admin:Key"];
            if (!string.IsNullOrEmpty(adminKey))
            {
                var provided = ctx.Request.Headers["X-Admin-Key"].FirstOrDefault();
                if (provided != adminKey)
                    return Results.Json(Error("unauthorized", "Invalid admin key."), statusCode: 401);
            }

            var requests = await db.AccessRequests
                .Where(r => r.Status == 0)
                .OrderBy(r => r.SubmittedAt)
                .Select(r => new { r.Id, r.Name, r.Email, r.Company, r.UseCase, r.SubmittedAt })
                .ToListAsync(ct);
            return Results.Ok(requests);
        });

        app.MapPost("/admin/requests/{id}/approve", async (
            int id, HttpContext ctx, IConfiguration cfg, SmilrDbContext db, IApiKeyService apiKeyService, CancellationToken ct) =>
        {
            var adminKey = cfg["Admin:Key"];
            if (!string.IsNullOrEmpty(adminKey))
            {
                var provided = ctx.Request.Headers["X-Admin-Key"].FirstOrDefault();
                if (provided != adminKey)
                    return Results.Json(Error("unauthorized", "Invalid admin key."), statusCode: 401);
            }

            var request = await db.AccessRequests.FindAsync([id], ct);
            if (request is null)
                return Results.NotFound(Error("not_found", $"No access request with id {id}."));
            if (request.Status != 0)
                return Results.BadRequest(Error("bad_request", "Request has already been processed."));

            var (plaintext, apiKey) = await apiKeyService.GenerateAsync(request.Email, "free", ct);
            request.Status   = 1;
            request.ApiKeyId = apiKey.Id;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { key = plaintext, email = request.Email, tier = "free" });
        });
    }

    private static object Error(string code, string message) =>
        new { error = new { code, message } };
}
