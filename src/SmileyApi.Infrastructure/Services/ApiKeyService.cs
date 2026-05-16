using Microsoft.EntityFrameworkCore;
using SmileyApi.Core.Interfaces;
using SmileyApi.Core.Models;
using SmileyApi.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace SmileyApi.Infrastructure.Services;

public class ApiKeyService(SmileyDbContext db) : IApiKeyService
{
    public async Task<ApiKey?> ValidateAsync(string rawKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return null;

        var hash = HashKey(rawKey);
        var apiKey = await db.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive, ct);

        if (apiKey is null) return null;

        if (apiKey.LastResetAt.Date < DateTime.UtcNow.Date)
        {
            apiKey.RequestsToday = 0;
            apiKey.LastResetAt = DateTime.UtcNow;
        }
        apiKey.RequestsToday++;
        await db.SaveChangesAsync(ct);

        return apiKey;
    }

    public async Task<string> GenerateAsync(string ownerEmail, string tier = "free", CancellationToken ct = default)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = Convert.ToBase64String(rawBytes)
                               .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        db.ApiKeys.Add(new ApiKey
        {
            KeyHash       = HashKey(plaintext),
            OwnerEmail    = ownerEmail,
            Tier          = tier,
            RequestsToday = 0,
            CreatedAt     = DateTime.UtcNow,
            LastResetAt   = DateTime.UtcNow,
            IsActive      = true
        });
        await db.SaveChangesAsync(ct);

        return plaintext;
    }

    private static string HashKey(string rawKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
}
