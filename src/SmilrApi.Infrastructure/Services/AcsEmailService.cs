using System.Net;
using System.Reflection;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;

namespace SmilrApi.Infrastructure.Services;

public class AcsEmailService : IEmailService
{
    private static readonly Lazy<string> LoginLinkTemplate = new(() => LoadTemplate("login-link-email.html"));
    private static readonly Lazy<string> VerifyAccountTemplate = new(() => LoadTemplate("verify-account-email.html"));

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

    private static string LoadTemplate(string fileName)
    {
        var assembly = typeof(AcsEmailService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded email template '{fileName}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Where a given send should actually go. When Email:OverrideAddress is set (test/QA use), everything
    /// is fully redirected there with a banner, same as always — no monitor CC on top, since the override
    /// address is typically the monitor address anyway and CC'ing it too would just double-send. When
    /// override is unset (the normal, real-recipient path), the real address is used and
    /// Email:SystemMonitorAddress (if configured and not identical to the recipient) is CC'd so ops keeps
    /// visibility into real outgoing mail without needing to hijack every send.
    /// </summary>
    internal readonly record struct RecipientPlan(string PrimaryRecipient, string Banner, IReadOnlyList<string> CcAddresses);

    internal RecipientPlan ResolveRecipient(string originalTo)
    {
        if (!string.IsNullOrWhiteSpace(_overrideAddress))
            return new RecipientPlan(_overrideAddress, $"[REDIRECT – originally for: {originalTo}]", Array.Empty<string>());

        var cc = !string.IsNullOrWhiteSpace(_systemMonitorAddress)
                 && !string.Equals(_systemMonitorAddress, originalTo, StringComparison.OrdinalIgnoreCase)
            ? new[] { _systemMonitorAddress! }
            : Array.Empty<string>();

        return new RecipientPlan(originalTo, string.Empty, cc);
    }

    private static EmailMessage BuildMessage(string sender, RecipientPlan plan, EmailContent content) =>
        new(senderAddress: sender,
            recipients: new EmailRecipients(
                to: new[] { new EmailAddress(plan.PrimaryRecipient) },
                cc: plan.CcAddresses.Select(a => new EmailAddress(a)),
                bcc: Array.Empty<EmailAddress>()),
            content: content);

    public async Task SendVerificationEmailAsync(string to, string companyName, string verifyUrl, CancellationToken ct = default)
    {
        var plan = ResolveRecipient(to);
        var banner = plan.Banner;
        var html = VerifyAccountTemplate.Value
            .Replace("{{UserName}}", WebUtility.HtmlEncode(companyName))
            .Replace("{{VerificationUrl}}", WebUtility.HtmlEncode(verifyUrl));
        if (banner.Length > 0)
            html = $"<p><strong>{banner}</strong></p>" + html;

        var message = BuildMessage(_sender, plan, new EmailContent("Verify your SmilrApi account")
        {
            Html = html,
            PlainText = (banner.Length > 0 ? $"{banner}\n\n" : "") +
                        $"Hello {companyName},\n\nVerify your SmilrApi account:\n{verifyUrl}\n\nThis link expires in 24 hours."
        });

        var op = await _client.SendAsync(WaitUntil.Started, message, ct);
        _logger.LogInformation("Verification email queued to {To} (cc={Cc}), operationId={Id}", plan.PrimaryRecipient, string.Join(",", plan.CcAddresses), op.Id);
    }

    public async Task SendMagicLinkEmailAsync(string to, string companyName, string loginUrl, CancellationToken ct = default)
    {
        var plan = ResolveRecipient(to);
        var banner = plan.Banner;
        var html = LoginLinkTemplate.Value
            .Replace("{{UserName}}", WebUtility.HtmlEncode(companyName))
            .Replace("{{LoginUrl}}", WebUtility.HtmlEncode(loginUrl));
        if (banner.Length > 0)
            html = $"<p><strong>{banner}</strong></p>" + html;

        var message = BuildMessage(_sender, plan, new EmailContent("Your SmilrApi login link")
        {
            Html = html,
            PlainText = (banner.Length > 0 ? $"{banner}\n\n" : "") +
                        $"Hi {companyName},\n\nSign in to SmilrApi:\n{loginUrl}\n\nExpires in 15 minutes. Ignore if you did not request this."
        });

        var op = await _client.SendAsync(WaitUntil.Started, message, ct);
        _logger.LogInformation("Magic link email queued to {To} (cc={Cc}), operationId={Id}", plan.PrimaryRecipient, string.Join(",", plan.CcAddresses), op.Id);
    }

    public async Task SendScoreAlertEmailAsync(string to, IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default)
    {
        var subject = changes.Count == 1
            ? $"Smilr score update: {changes[0].EstablishmentName}"
            : $"Smilr score update: {changes.Count} establishments changed";

        var plan = ResolveRecipient(to);
        var banner = plan.Banner;

        var rows = string.Concat(changes.Select(c =>
            $"<tr>" +
            $"<td>{c.CvrNumber ?? "—"}</td>" +
            $"<td>{c.EstablishmentName}</td>" +
            $"<td>{c.Address ?? "—"}</td>" +
            $"<td>{c.OldScore} → {c.NewScore}</td>" +
            $"</tr>"));

        var plainLines = string.Join("\n", changes.Select(c =>
            $"{c.CvrNumber ?? "—"} | {c.EstablishmentName} | {c.Address ?? "—"} | {c.OldScore} → {c.NewScore}"));

        var message = BuildMessage(_sender, plan, new EmailContent(subject)
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
        _logger.LogInformation("Score alert email queued to {To} (cc={Cc}) ({Count} changes), operationId={Id}", plan.PrimaryRecipient, string.Join(",", plan.CcAddresses), changes.Count, op.Id);
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
