using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;

namespace SmilrApi.Infrastructure.Services;

public class AcsEmailService : IEmailService
{
    private readonly EmailClient _client;
    private readonly string _sender;
    private readonly string? _overrideAddress;
    private readonly string? _systemMonitorAddress;
    private readonly ILogger<AcsEmailService> _logger;

    public AcsEmailService(IConfiguration config, ILogger<AcsEmailService> logger)
    {
        _logger = logger;
        _client = new EmailClient(
            config["Acs:ConnectionString"]
                ?? throw new InvalidOperationException("Acs:ConnectionString is not configured."));
        _sender = config["Acs:SenderAddress"] ?? "donotreply@smilrhq.dk";
        _overrideAddress = config["Email:OverrideAddress"];
        _systemMonitorAddress = config["Email:SystemMonitorAddress"];
    }

    private (string recipient, string banner) ResolveRecipient(string originalTo)
    {
        if (string.IsNullOrWhiteSpace(_overrideAddress))
            return (originalTo, string.Empty);
        return (_overrideAddress, $"[REDIRECT – originally for: {originalTo}]");
    }

    public async Task SendVerificationEmailAsync(string to, string verifyUrl, CancellationToken ct = default)
    {
        var (recipient, banner) = ResolveRecipient(to);
        var message = new EmailMessage(
            senderAddress: _sender,
            recipientAddress: recipient,
            content: new EmailContent("Verify your SmilrApi account")
            {
                Html = (banner.Length > 0 ? $"<p><strong>{banner}</strong></p>" : "") +
                       "<p>Click the link below to verify your email and activate your SmilrApi account:</p>" +
                       $"<p><a href=\"{verifyUrl}\">{verifyUrl}</a></p>" +
                       "<p>This link expires in 24 hours.</p>",
                PlainText = (banner.Length > 0 ? $"{banner}\n\n" : "") +
                            $"Verify your SmilrApi account:\n{verifyUrl}\n\nThis link expires in 24 hours."
            });

        var op = await _client.SendAsync(WaitUntil.Started, message, ct);
        _logger.LogInformation("Verification email queued to {To}, operationId={Id}", recipient, op.Id);
    }

    public async Task SendMagicLinkEmailAsync(string to, string loginUrl, CancellationToken ct = default)
    {
        var (recipient, banner) = ResolveRecipient(to);
        var message = new EmailMessage(
            senderAddress: _sender,
            recipientAddress: recipient,
            content: new EmailContent("Your SmilrApi login link")
            {
                Html = (banner.Length > 0 ? $"<p><strong>{banner}</strong></p>" : "") +
                       "<p>Click the link below to sign in to your SmilrApi dashboard:</p>" +
                       $"<p><a href=\"{loginUrl}\">{loginUrl}</a></p>" +
                       "<p>This link expires in 15 minutes. If you did not request this, you can ignore this email.</p>",
                PlainText = (banner.Length > 0 ? $"{banner}\n\n" : "") +
                            $"Sign in to SmilrApi:\n{loginUrl}\n\nExpires in 15 minutes. Ignore if you did not request this."
            });

        var op = await _client.SendAsync(WaitUntil.Started, message, ct);
        _logger.LogInformation("Magic link email queued to {To}, operationId={Id}", recipient, op.Id);
    }

    public async Task SendScoreAlertEmailAsync(string to, IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default)
    {
        var subject = changes.Count == 1
            ? $"Smilr score update: {changes[0].EstablishmentName}"
            : $"Smilr score update: {changes.Count} establishments changed";

        var (recipient, banner) = ResolveRecipient(to);

        var rows = string.Concat(changes.Select(c =>
            $"<tr>" +
            $"<td>{c.CvrNumber ?? "—"}</td>" +
            $"<td>{c.EstablishmentName}</td>" +
            $"<td>{c.Address ?? "—"}</td>" +
            $"<td>{c.OldScore} → {c.NewScore}</td>" +
            $"</tr>"));

        var plainLines = string.Join("\n", changes.Select(c =>
            $"{c.CvrNumber ?? "—"} | {c.EstablishmentName} | {c.Address ?? "—"} | {c.OldScore} → {c.NewScore}"));

        var message = new EmailMessage(
            senderAddress: _sender,
            recipientAddress: recipient,
            content: new EmailContent(subject)
            {
                Html = (banner.Length > 0 ? $"<p><strong>{banner}</strong></p>" : "") +
                       "<p>The following Smilr scores have changed:</p>" +
                       "<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse\">" +
                       "<thead><tr><th>CVR</th><th>Name</th><th>Address</th><th>Score change</th></tr></thead>" +
                       $"<tbody>{rows}</tbody></table>" +
                       "<p>Log in to your <a href=\"https://smilrhq.dk/dashboard.html\">Smilr dashboard</a> for details.</p>",
                PlainText = (banner.Length > 0 ? $"{banner}\n\n" : "") +
                            $"Smilr score updates:\n\n{plainLines}"
            });

        var op = await _client.SendAsync(WaitUntil.Started, message, ct);
        _logger.LogInformation("Score alert email queued to {To} ({Count} changes), operationId={Id}", recipient, changes.Count, op.Id);
    }

    public async Task SendSystemScoreDigestAsync(IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_systemMonitorAddress))
        {
            _logger.LogDebug("Email:SystemMonitorAddress is not configured; skipping system score digest ({Count} change(s)).", changes.Count);
            return;
        }

        var subject = $"Smilr system digest: {changes.Count} score change(s) nationwide";

        var rows = string.Concat(changes.Select(c =>
            $"<tr>" +
            $"<td>{c.CvrNumber ?? "—"}</td>" +
            $"<td>{c.EstablishmentName}</td>" +
            $"<td>{c.Address ?? "—"}</td>" +
            $"<td>{c.OldScore} → {c.NewScore}</td>" +
            $"</tr>"));

        var plainLines = string.Join("\n", changes.Select(c =>
            $"{c.CvrNumber ?? "—"} | {c.EstablishmentName} | {c.Address ?? "—"} | {c.OldScore} → {c.NewScore}"));

        var message = new EmailMessage(
            senderAddress: _sender,
            recipientAddress: _systemMonitorAddress,
            content: new EmailContent(subject)
            {
                Html = "<p>Every establishment nationwide whose Smiley score changed in this sync (not limited to establishments tracked by a business account):</p>" +
                       "<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse\">" +
                       "<thead><tr><th>CVR</th><th>Name</th><th>Address</th><th>Score change</th></tr></thead>" +
                       $"<tbody>{rows}</tbody></table>",
                PlainText = $"Smilr system-wide score digest ({changes.Count} change(s)):\n\n{plainLines}"
            });

        var op = await _client.SendAsync(WaitUntil.Started, message, ct);
        _logger.LogInformation("System score digest email queued to {To} ({Count} changes), operationId={Id}", _systemMonitorAddress, changes.Count, op.Id);
    }
}
