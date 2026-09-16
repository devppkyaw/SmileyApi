using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Infrastructure.Data;

namespace SmilrApi.Infrastructure.Services;

public class BusinessService(SmilrDbContext db, IEmailService emailService) : IBusinessService
{
    public async Task<Business?> RegisterOrResendAsync(
        string email, string companyName, bool marketingConsent, string baseUrl, string? claimCvr = null, CancellationToken ct = default)
    {
        var existing = await db.Businesses.FirstOrDefaultAsync(b => b.Email == email, ct);

        if (existing is not null && existing.IsEmailVerified)
            return null; // already verified — caller returns 409

        var token  = GenerateToken();
        var expiry = DateTime.UtcNow.AddHours(24);

        if (existing is not null)
        {
            existing.MagicLinkToken       = token;
            existing.MagicLinkTokenExpiry = expiry;
        }
        else
        {
            existing = new Business
            {
                BusinessId            = GenerateBusinessId(),
                Email                 = email.Trim(),
                CompanyName           = companyName.Trim(),
                Tier                  = "free",
                IsEmailVerified       = false,
                MagicLinkToken        = token,
                MagicLinkTokenExpiry  = expiry,
                CreatedAt             = DateTime.UtcNow,
                TermsAcceptedAt       = DateTime.UtcNow,
                MarketingConsentGiven = marketingConsent,
                MarketingConsentAt    = marketingConsent ? DateTime.UtcNow : null
            };
            db.Businesses.Add(existing);
        }

        // Only overwrite when a claim was actually passed — a plain resend without the param
        // (e.g. clicking "resend" on the check-your-email screen) must not wipe an intent that
        // was captured on an earlier submission.
        if (!string.IsNullOrWhiteSpace(claimCvr))
            existing.PendingClaimCvr = claimCvr.Trim();

        await db.SaveChangesAsync(ct);
        await emailService.SendVerificationEmailAsync(
            existing.Email, existing.CompanyName, $"{baseUrl}/v1/business/verify?token={token}", ct);

        return existing;
    }

    public async Task<Business?> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(
            b => b.MagicLinkToken == token && b.MagicLinkTokenExpiry > DateTime.UtcNow, ct);

        if (business is null) return null;

        business.IsEmailVerified      = true;
        business.VerifiedAt           = DateTime.UtcNow;
        business.MagicLinkToken       = null;
        business.MagicLinkTokenExpiry = null;

        // Capture/clear/restore: persist the claim as consumed so it never re-fires on a later
        // login, but hand the original value back to the caller (BusinessEndpoints builds the
        // post-verify redirect from it) since the in-memory property is now null after saving.
        var pendingClaimCvr        = business.PendingClaimCvr;
        business.PendingClaimCvr   = null;
        await db.SaveChangesAsync(ct);
        business.PendingClaimCvr = pendingClaimCvr;
        return business;
    }

    public async Task<bool> RequestMagicLinkAsync(string email, string baseUrl, string? claimCvr = null, CancellationToken ct = default)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(
            b => b.Email == email, ct);

        if (business is null) return false;

        var token = GenerateToken();
        business.MagicLinkToken = token;

        if (!string.IsNullOrWhiteSpace(claimCvr))
            business.PendingClaimCvr = claimCvr.Trim();

        if (!business.IsEmailVerified)
        {
            business.MagicLinkTokenExpiry = DateTime.UtcNow.AddHours(24);
            await db.SaveChangesAsync(ct);
            await emailService.SendVerificationEmailAsync(
                business.Email, business.CompanyName, $"{baseUrl}/v1/business/verify?token={token}", ct);
            return true;
        }

        business.MagicLinkTokenExpiry = DateTime.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync(ct);
        await emailService.SendMagicLinkEmailAsync(
            business.Email, business.CompanyName, $"{baseUrl}/v1/business/login/verify?token={token}", ct);
        return true;
    }

    public async Task<Business?> VerifyMagicLinkAsync(string token, CancellationToken ct = default)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(
            b => b.MagicLinkToken == token && b.MagicLinkTokenExpiry > DateTime.UtcNow, ct);

        if (business is null) return null;

        business.MagicLinkToken       = null;
        business.MagicLinkTokenExpiry = null;

        var pendingClaimCvr      = business.PendingClaimCvr;
        business.PendingClaimCvr = null;
        await db.SaveChangesAsync(ct);
        business.PendingClaimCvr = pendingClaimCvr;
        return business;
    }

    public Task<Business?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Businesses.FirstOrDefaultAsync(b => b.Id == id, ct);

    private static string GenerateBusinessId() =>
        "biz_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLower();

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower();
}
