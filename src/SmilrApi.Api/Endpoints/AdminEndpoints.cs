using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Interfaces;
using SmilrApi.Infrastructure.Data;

namespace SmilrApi.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        if (app.Environment.IsProduction()) return;

        app.MapGet("/admin/requests", async (SmileyDbContext db, CancellationToken ct) =>
        {
            var requests = await db.AccessRequests
                .Where(r => r.Status == 0)
                .OrderBy(r => r.SubmittedAt)
                .Select(r => new { r.Id, r.Name, r.Email, r.Company, r.UseCase, r.SubmittedAt })
                .ToListAsync(ct);
            return Results.Ok(requests);
        });

        app.MapPost("/admin/requests/{id}/approve", async (
            int id, SmileyDbContext db, IApiKeyService apiKeyService, CancellationToken ct) =>
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
