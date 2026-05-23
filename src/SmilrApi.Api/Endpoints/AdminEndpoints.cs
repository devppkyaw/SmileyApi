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
        if (app.Environment.IsProduction()) return;

        app.MapGet("/admin/requests", async (SmilrDbContext db, CancellationToken ct) =>
        {
            var requests = await db.AccessRequests
                .Where(r => r.Status == 0)
                .OrderBy(r => r.SubmittedAt)
                .Select(r => new { r.Id, r.Name, r.Email, r.Company, r.UseCase, r.SubmittedAt })
                .ToListAsync(ct);
            return Results.Ok(requests);
        });

        app.MapPost("/admin/sync", async (
            FodevareXmlParser parser,
            IServiceScopeFactory scopeFactory,
            CancellationToken ct) =>
        {
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

        app.MapPost("/admin/requests/{id}/approve", async (
            int id, SmilrDbContext db, IApiKeyService apiKeyService, CancellationToken ct) =>
        {
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
