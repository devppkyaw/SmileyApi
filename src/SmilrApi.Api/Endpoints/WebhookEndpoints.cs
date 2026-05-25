using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Infrastructure.Services;

namespace SmilrApi.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var v1 = app.MapGroup("/v1/webhooks")
                    .WithTags("Public")
                    .RequireRateLimiting("api-key-tier");

        v1.MapPost("/", async (WebhookSubscribeRequest req, HttpContext ctx, WebhookService svc, CancellationToken ct) =>
        {
            var apiKey = ctx.Items["ApiKey"] as ApiKey;
            if (apiKey is null)
                return Error(401, "unauthorized", "Valid API key required.");

            if (string.IsNullOrWhiteSpace(req.CallbackUrl) || !req.CallbackUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Error(400, "bad_request", "callbackUrl must be a valid HTTPS URL.");

            try
            {
                var (sub, secret) = await svc.SubscribeAsync(apiKey.Id, req.EstablishmentId, req.CallbackUrl, ct);
                return Results.Created($"/v1/webhooks/{sub.Id}", new WebhookCreatedDto(
                    sub.Id, sub.EstablishmentId, sub.CallbackUrl, secret, sub.CreatedAt));
            }
            catch (KeyNotFoundException ex)
            {
                return Error(404, "not_found", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Error(409, "conflict", ex.Message);
            }
        });

        v1.MapDelete("/{id:int}", async (int id, HttpContext ctx, WebhookService svc, CancellationToken ct) =>
        {
            var apiKey = ctx.Items["ApiKey"] as ApiKey;
            if (apiKey is null)
                return Error(401, "unauthorized", "Valid API key required.");

            var deleted = await svc.UnsubscribeAsync(id, apiKey.Id, ct);
            return deleted ? Results.NoContent() : Error(404, "not_found", $"Subscription {id} not found or does not belong to your API key.");
        });

        v1.MapGet("/", async (HttpContext ctx, WebhookService svc, CancellationToken ct) =>
        {
            var apiKey = ctx.Items["ApiKey"] as ApiKey;
            if (apiKey is null)
                return Error(401, "unauthorized", "Valid API key required.");

            var subs = await svc.GetByApiKeyAsync(apiKey.Id, ct);
            return Results.Ok(subs.Select(s => new WebhookSummaryDto(s.Id, s.EstablishmentId, s.CallbackUrl, s.CreatedAt)));
        });

        // Business session-auth webhook management (Pro only)
        const string bizSessionKey = "business_id";

        var biz = app.MapGroup("/v1/business/webhooks");

        biz.MapPost("/", async (
            WebhookSubscribeRequest req,
            HttpContext ctx,
            IBusinessService businessSvc,
            WebhookService svc,
            CancellationToken ct) =>
        {
            var id       = ctx.Session.GetInt32(bizSessionKey);
            if (id is null) return Error(401, "unauthorized", "Login required.");
            var business = await businessSvc.GetByIdAsync(id.Value, ct);
            if (business is null || !business.IsEmailVerified) return Error(401, "unauthorized", "Login required.");
            if (business.Tier != "pro") return Error(403, "forbidden", "Webhooks require a Pro account.");

            if (string.IsNullOrWhiteSpace(req.CallbackUrl) || !req.CallbackUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Error(400, "bad_request", "callbackUrl must be a valid HTTPS URL.");

            try
            {
                var (sub, secret) = await svc.SubscribeBusinessAsync(business.Id, req.EstablishmentId, req.CallbackUrl, ct);
                return Results.Created($"/v1/business/webhooks/{sub.Id}", new WebhookCreatedDto(
                    sub.Id, sub.EstablishmentId, sub.CallbackUrl, secret, sub.CreatedAt));
            }
            catch (KeyNotFoundException ex)      { return Error(404, "not_found", ex.Message); }
            catch (InvalidOperationException ex) { return Error(409, "conflict", ex.Message); }
        });

        biz.MapDelete("/{id:int}", async (
            int id,
            HttpContext ctx,
            IBusinessService businessSvc,
            WebhookService svc,
            CancellationToken ct) =>
        {
            var sessionId = ctx.Session.GetInt32(bizSessionKey);
            if (sessionId is null) return Error(401, "unauthorized", "Login required.");
            var business  = await businessSvc.GetByIdAsync(sessionId.Value, ct);
            if (business is null || !business.IsEmailVerified) return Error(401, "unauthorized", "Login required.");
            if (business.Tier != "pro") return Error(403, "forbidden", "Webhooks require a Pro account.");

            var deleted = await svc.UnsubscribeBusinessAsync(id, business.Id, ct);
            return deleted ? Results.NoContent() : Error(404, "not_found", $"Subscription {id} not found.");
        });

        biz.MapGet("/", async (
            HttpContext ctx,
            IBusinessService businessSvc,
            WebhookService svc,
            CancellationToken ct) =>
        {
            var id       = ctx.Session.GetInt32(bizSessionKey);
            if (id is null) return Error(401, "unauthorized", "Login required.");
            var business = await businessSvc.GetByIdAsync(id.Value, ct);
            if (business is null || !business.IsEmailVerified) return Error(401, "unauthorized", "Login required.");
            if (business.Tier != "pro") return Error(403, "forbidden", "Webhooks require a Pro account.");

            var subs = await svc.GetByBusinessAsync(business.Id, ct);
            return Results.Ok(subs.Select(s => new WebhookSummaryDto(s.Id, s.EstablishmentId, s.CallbackUrl, s.CreatedAt)));
        });
    }

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);
}

public record WebhookSubscribeRequest(int EstablishmentId, string CallbackUrl);

// Secret is returned only on creation — never retrievable again
public record WebhookCreatedDto(int Id, int EstablishmentId, string CallbackUrl, string Secret, DateTime CreatedAt);

public record WebhookSummaryDto(int Id, int EstablishmentId, string CallbackUrl, DateTime CreatedAt);
