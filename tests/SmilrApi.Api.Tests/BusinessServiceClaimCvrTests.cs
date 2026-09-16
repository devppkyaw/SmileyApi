using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;
using SmilrApi.Infrastructure.Data;
using SmilrApi.Infrastructure.Services;

namespace SmilrApi.Api.Tests;

// Covers PendingClaimCvr threading through registration/login/verification — the anonymous-user
// half of the "Claim this listing" fix (the already-logged-in half, in register.html/
// dashboard.html client JS, has no C# to unit test; see PR description).
public class BusinessServiceClaimCvrTests
{
    private static SmilrDbContext NewDb() =>
        new(new DbContextOptionsBuilder<SmilrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BusinessService NewService(SmilrDbContext db) =>
        new(db, new NoOpEmailService());

    [Fact]
    public async Task RegisterOrResendAsync_NewBusiness_WithClaimCvr_SetsPendingClaimCvr()
    {
        using var db  = NewDb();
        var svc       = NewService(db);

        var business = await svc.RegisterOrResendAsync(
            "new@example.com", "New Co", marketingConsent: false, baseUrl: "http://localhost",
            claimCvr: "12345678");

        Assert.NotNull(business);
        Assert.Equal("12345678", business!.PendingClaimCvr);
    }

    [Fact]
    public async Task RegisterOrResendAsync_ResendForUnverifiedBusiness_WithClaimCvr_UpdatesPendingClaimCvr()
    {
        using var db = NewDb();
        var svc      = NewService(db);

        await svc.RegisterOrResendAsync("resend@example.com", "Co", false, "http://localhost", claimCvr: "11111111");
        var second = await svc.RegisterOrResendAsync("resend@example.com", "Co", false, "http://localhost", claimCvr: "22222222");

        Assert.NotNull(second);
        Assert.Equal("22222222", second!.PendingClaimCvr);
    }

    [Fact]
    public async Task RegisterOrResendAsync_ResendWithoutClaimCvr_PreservesPreviouslyStoredValue()
    {
        using var db = NewDb();
        var svc      = NewService(db);

        await svc.RegisterOrResendAsync("keep@example.com", "Co", false, "http://localhost", claimCvr: "33333333");
        var second = await svc.RegisterOrResendAsync("keep@example.com", "Co", false, "http://localhost", claimCvr: null);

        Assert.NotNull(second);
        Assert.Equal("33333333", second!.PendingClaimCvr);
    }

    [Fact]
    public async Task VerifyEmailAsync_WithPendingClaim_ReturnsItButClearsPersistedValue()
    {
        using var db = NewDb();
        var svc      = NewService(db);

        var registered = await svc.RegisterOrResendAsync(
            "verify@example.com", "Co", false, "http://localhost", claimCvr: "44444444");
        var token = registered!.MagicLinkToken!;

        var verified = await svc.VerifyEmailAsync(token);

        Assert.NotNull(verified);
        Assert.Equal("44444444", verified!.PendingClaimCvr);

        var persisted = await db.Businesses.AsNoTracking()
            .SingleAsync(b => b.Email == "verify@example.com");
        Assert.Null(persisted.PendingClaimCvr);
    }

    [Fact]
    public async Task VerifyEmailAsync_WithNoPendingClaim_RoundTripsNull()
    {
        using var db = NewDb();
        var svc      = NewService(db);

        var registered = await svc.RegisterOrResendAsync("noclaim@example.com", "Co", false, "http://localhost");
        var verified    = await svc.VerifyEmailAsync(registered!.MagicLinkToken!);

        Assert.NotNull(verified);
        Assert.Null(verified!.PendingClaimCvr);
    }

    [Fact]
    public async Task RequestMagicLinkAsync_UnverifiedBusiness_WithClaimCvr_SetsPendingClaimCvr()
    {
        using var db = NewDb();
        var svc      = NewService(db);

        await svc.RegisterOrResendAsync("login-unverified@example.com", "Co", false, "http://localhost");
        var sent = await svc.RequestMagicLinkAsync("login-unverified@example.com", "http://localhost", claimCvr: "55555555");

        Assert.True(sent);
        var persisted = await db.Businesses.AsNoTracking()
            .SingleAsync(b => b.Email == "login-unverified@example.com");
        Assert.Equal("55555555", persisted.PendingClaimCvr);
    }

    [Fact]
    public async Task RequestMagicLinkAsync_VerifiedBusiness_WithClaimCvr_SetsPendingClaimCvr()
    {
        using var db = NewDb();
        var svc      = NewService(db);

        var registered = await svc.RegisterOrResendAsync("login-verified@example.com", "Co", false, "http://localhost");
        await svc.VerifyEmailAsync(registered!.MagicLinkToken!);

        var sent = await svc.RequestMagicLinkAsync("login-verified@example.com", "http://localhost", claimCvr: "66666666");

        Assert.True(sent);
        var persisted = await db.Businesses.AsNoTracking()
            .SingleAsync(b => b.Email == "login-verified@example.com");
        Assert.Equal("66666666", persisted.PendingClaimCvr);
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_WithPendingClaim_ReturnsItButClearsPersistedValue()
    {
        using var db = NewDb();
        var svc      = NewService(db);

        var registered = await svc.RegisterOrResendAsync("magiclink@example.com", "Co", false, "http://localhost");
        await svc.VerifyEmailAsync(registered!.MagicLinkToken!);
        await svc.RequestMagicLinkAsync("magiclink@example.com", "http://localhost", claimCvr: "77777777");

        var withToken = await db.Businesses.AsNoTracking().SingleAsync(b => b.Email == "magiclink@example.com");
        var loggedIn  = await svc.VerifyMagicLinkAsync(withToken.MagicLinkToken!);

        Assert.NotNull(loggedIn);
        Assert.Equal("77777777", loggedIn!.PendingClaimCvr);

        var persisted = await db.Businesses.AsNoTracking()
            .SingleAsync(b => b.Email == "magiclink@example.com");
        Assert.Null(persisted.PendingClaimCvr);
    }

    [Fact]
    public async Task VerifyMagicLinkAsync_WithNoPendingClaim_RoundTripsNull()
    {
        using var db = NewDb();
        var svc      = NewService(db);

        var registered = await svc.RegisterOrResendAsync("magiclink-noclaim@example.com", "Co", false, "http://localhost");
        await svc.VerifyEmailAsync(registered!.MagicLinkToken!);
        await svc.RequestMagicLinkAsync("magiclink-noclaim@example.com", "http://localhost");

        var withToken = await db.Businesses.AsNoTracking().SingleAsync(b => b.Email == "magiclink-noclaim@example.com");
        var loggedIn  = await svc.VerifyMagicLinkAsync(withToken.MagicLinkToken!);

        Assert.NotNull(loggedIn);
        Assert.Null(loggedIn!.PendingClaimCvr);
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendVerificationEmailAsync(string to, string companyName, string verifyUrl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendMagicLinkEmailAsync(string to, string companyName, string loginUrl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendScoreAlertEmailAsync(string to, IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSystemScoreDigestAsync(IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendFeedHealthAlertAsync(
            FeedHealthAlertKind kind, string? fieldName, FeedHealthAlertStatus status,
            string summary, DateTime? firstDetectedAt, CancellationToken ct = default) => Task.CompletedTask;
    }
}
