using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace SmilrApi.Infrastructure.Services;

public class ApiKeyService(SmilrDbContext db) : IApiKeyService
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

    public async Task<(string Plaintext, ApiKey Key)> GenerateAsync(string ownerEmail, string tier = "free", CancellationToken ct = default)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = Convert.ToBase64String(rawBytes)
                               .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var apiKey = new ApiKey
        {
            KeyHash       = HashKey(plaintext),
            OwnerEmail    = ownerEmail,
            Tier          = tier,
            RequestsToday = 0,
            CreatedAt     = DateTime.UtcNow,
            LastResetAt   = DateTime.UtcNow,
            IsActive      = true
        };
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(ct);

        return (plaintext, apiKey);
    }

    public async Task<(string Plaintext, ApiKey Key)> GenerateForBusinessAsync(Business business, CancellationToken ct = default)
    {
        var existing = await db.ApiKeys.FirstOrDefaultAsync(
            k => k.BusinessId == business.Id && k.IsActive, ct);
        if (existing is not null)
        {
            existing.IsActive = false;
            existing.RevokedAt = DateTimeOffset.UtcNow;
        }

        var rawBytes  = RandomNumberGenerator.GetBytes(32);
        var plaintext = Convert.ToBase64String(rawBytes)
                               .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var apiKey = new ApiKey
        {
            KeyHash       = HashKey(plaintext),
            OwnerEmail    = business.Email,
            Tier          = business.Tier,
            BusinessId    = business.Id,
            RequestsToday = 0,
            CreatedAt     = DateTime.UtcNow,
            LastResetAt   = DateTime.UtcNow,
            IsActive      = true
        };
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(ct);

        return (plaintext, apiKey);
    }

    public async Task<ApiKey?> GetForBusinessAsync(int businessId, CancellationToken ct = default) =>
        await db.ApiKeys.FirstOrDefaultAsync(k => k.BusinessId == businessId && k.IsActive, ct);

    public async Task<ApiKey?> RevokeForBusinessAsync(int businessId, CancellationToken ct = default)
    {
        var apiKey = await db.ApiKeys.FirstOrDefaultAsync(
            k => k.BusinessId == businessId && k.IsActive, ct);
        if (apiKey is null) return null;

        apiKey.IsActive = false;
        apiKey.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return apiKey;
    }

    private static string HashKey(string rawKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
}
