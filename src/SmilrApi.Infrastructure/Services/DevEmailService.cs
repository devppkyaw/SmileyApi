using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;

namespace SmilrApi.Infrastructure.Services;

public class DevEmailService(ILogger<DevEmailService> logger, IConfiguration config) : IEmailService
{
    private string? Override => config["Email:OverrideAddress"] is { Length: > 0 } v ? v : null;

    private string ToInfo(string originalTo) =>
        Override is { } ov ? $"REDIRECTED to {ov} (originally {originalTo})" : originalTo;

    public Task SendVerificationEmailAsync(string to, string verifyUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Verification → {To} | {Url}", ToInfo(to), verifyUrl);
        return Task.CompletedTask;
    }

    public Task SendMagicLinkEmailAsync(string to, string loginUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Magic link → {To} | {Url}", ToInfo(to), loginUrl);
        return Task.CompletedTask;
    }

    public Task SendScoreAlertEmailAsync(string to, IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default)
    {
        var detail = string.Join(", ", changes.Select(c => $"{c.EstablishmentName} ({c.OldScore}→{c.NewScore})"));
        logger.LogInformation("[DEV EMAIL] Score alert → {To} | {Count} change(s): {Detail}", ToInfo(to), changes.Count, detail);
        return Task.CompletedTask;
    }
}
