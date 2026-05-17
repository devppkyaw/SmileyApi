using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmileyApi.Core.Models;
using SmileyApi.Infrastructure.Data;
using SmileyApi.Infrastructure.Jobs;
using System.Security.Cryptography;

namespace SmileyApi.Infrastructure.Services;

public record WebhookScoreChange(int EstablishmentId, int OldScore, int NewScore);

public class WebhookService(SmileyDbContext db, IBackgroundJobClient jobs, ILogger<WebhookService> logger)
{
    public async Task<(WebhookSubscription subscription, string secret)> SubscribeAsync(
        int apiKeyId, int establishmentId, string callbackUrl, CancellationToken ct)
    {
        if (!await db.Establishments.AnyAsync(e => e.Id == establishmentId, ct))
            throw new KeyNotFoundException($"Establishment {establishmentId} not found.");

        bool duplicate = await db.WebhookSubscriptions.AnyAsync(
            s => s.ApiKeyId == apiKeyId && s.EstablishmentId == establishmentId && s.CallbackUrl == callbackUrl, ct);
        if (duplicate)
            throw new InvalidOperationException("A subscription with the same establishment and callback URL already exists.");

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var sub = new WebhookSubscription
        {
            ApiKeyId = apiKeyId,
            EstablishmentId = establishmentId,
            CallbackUrl = callbackUrl,
            SecretKey = secret,
            CreatedAt = DateTime.UtcNow
        };
        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync(ct);
        return (sub, secret);
    }

    public async Task<bool> UnsubscribeAsync(int subscriptionId, int apiKeyId, CancellationToken ct)
    {
        var sub = await db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (sub is null) return false;
        if (sub.ApiKeyId != apiKeyId) return false;

        db.WebhookSubscriptions.Remove(sub);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<WebhookSubscription>> GetByApiKeyAsync(int apiKeyId, CancellationToken ct) =>
        db.WebhookSubscriptions
          .Where(s => s.ApiKeyId == apiKeyId)
          .OrderByDescending(s => s.CreatedAt)
          .ToListAsync(ct);

    public async Task EnqueueDeliveriesAsync(IReadOnlyList<WebhookScoreChange> changes, CancellationToken ct)
    {
        var establishmentIds = changes.Select(c => c.EstablishmentId).Distinct().ToList();
        var subscriptions = await db.WebhookSubscriptions
            .Where(s => establishmentIds.Contains(s.EstablishmentId))
            .ToListAsync(ct);

        if (subscriptions.Count == 0) return;

        var changeMap = changes.ToDictionary(c => c.EstablishmentId);
        int enqueued = 0;
        foreach (var sub in subscriptions)
        {
            var change = changeMap[sub.EstablishmentId];
            jobs.Enqueue<WebhookDeliveryJob>(j => j.DeliverAsync(sub.Id, change.OldScore, change.NewScore));
            enqueued++;
        }

        logger.LogInformation("Enqueued {Count} webhook delivery job(s) for {Changes} score change(s).",
            enqueued, changes.Count);
    }
}
