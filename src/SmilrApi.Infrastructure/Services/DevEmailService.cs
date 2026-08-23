using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;

namespace SmilrApi.Infrastructure.Services;

public class DevEmailService(ILogger<DevEmailService> logger, IConfiguration config) : IEmailService
{
    private string? Override => config["Email:OverrideAddress"] is { Length: > 0 } v ? v : null;
    private string? SystemMonitor => config["Email:SystemMonitorAddress"] is { Length: > 0 } v ? v : null;

    private string ToInfo(string originalTo)
    {
        if (Override is { } ov)
            return $"REDIRECTED to {ov} (originally {originalTo})";

        return SystemMonitor is { } monitor && !string.Equals(monitor, originalTo, StringComparison.OrdinalIgnoreCase)
            ? $"{originalTo} (cc {monitor})"
            : originalTo;
    }

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

    public Task SendSystemScoreDigestAsync(IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default)
    {
        var systemAddress = config["Email:SystemMonitorAddress"] is { Length: > 0 } v ? v : null;
        if (systemAddress is null)
        {
            logger.LogInformation("[DEV EMAIL] Email:SystemMonitorAddress not configured; skipping system score digest ({Count} change(s)).", changes.Count);
            return Task.CompletedTask;
        }

        var detail = string.Join(", ", changes.Select(c => $"{c.EstablishmentName} ({c.OldScore}→{c.NewScore})"));
        logger.LogInformation("[DEV EMAIL] System score digest → {To} | {Count} change(s): {Detail}", systemAddress, changes.Count, detail);
        return Task.CompletedTask;
    }
}
