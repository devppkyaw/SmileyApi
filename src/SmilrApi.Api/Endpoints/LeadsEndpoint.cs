using SmilrApi.Core.Models;
using SmilrApi.Infrastructure.Data;

namespace SmilrApi.Api.Endpoints;

public static class LeadsEndpoint
{
    public static void MapLeadsEndpoint(this WebApplication app)
    {
        if (app.Environment.IsProduction()) return;

        app.MapPost("/v1/leads", async (LeadRequest req, SmilrDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(Error("bad_request", "'name' is required."));
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(Error("bad_request", "'email' is required."));
            if (string.IsNullOrWhiteSpace(req.UseCase))
                return Results.BadRequest(Error("bad_request", "'useCase' is required."));

            db.AccessRequests.Add(new AccessRequest
            {
                Name        = req.Name.Trim(),
                Email       = req.Email.Trim(),
                Company     = req.Company?.Trim(),
                UseCase     = req.UseCase.Trim(),
                SubmittedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Request received. We'll email you within 24 hours." });
        });
    }

    private static object Error(string code, string message) =>
        new { error = new { code, message } };
}

public record LeadRequest(string Name, string Email, string? Company, string UseCase);
