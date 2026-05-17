using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmileyApi.Infrastructure.Data;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmileyApi.Infrastructure.Jobs;

public class WebhookDeliveryJob(SmileyDbContext db, IHttpClientFactory httpFactory, ILogger<WebhookDeliveryJob> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task DeliverAsync(int subscriptionId, int oldScore, int newScore)
    {
        var sub = await db.WebhookSubscriptions
            .Include(s => s.Establishment)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (sub is null)
        {
            logger.LogWarning("Webhook subscription {Id} no longer exists — skipping delivery.", subscriptionId);
            return;
        }

        var payload = new
        {
            @event = "smiley_score_changed",
            occurredAt = DateTime.UtcNow,
            establishment = new
            {
                id = sub.Establishment.Id,
                navnelbnr = sub.Establishment.Navnelbnr,
                cvr = sub.Establishment.CvrNumber,
                name = sub.Establishment.Name
            },
            currentScore = newScore,
            previousScore = oldScore
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var signature = ComputeHmac(sub.SecretKey, json);

        using var client = httpFactory.CreateClient("webhook");
        var request = new HttpRequestMessage(HttpMethod.Post, sub.CallbackUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Smiley-Signature", $"sha256={signature}");
        request.Headers.Add("X-Smiley-Event", "smiley_score_changed");

        var response = await client.SendAsync(request);

        logger.LogInformation(
            "Webhook delivery to {Url} for subscription {Id}: HTTP {Status}",
            sub.CallbackUrl, subscriptionId, (int)response.StatusCode);

        response.EnsureSuccessStatusCode();
    }

    private static string ComputeHmac(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
