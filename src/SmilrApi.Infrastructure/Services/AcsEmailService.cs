using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmilrApi.Core.Interfaces;

namespace SmilrApi.Infrastructure.Services;

public class AcsEmailService(IConfiguration config, ILogger<AcsEmailService> logger) : IEmailService
{
    private EmailClient Client =>
        new(config["Acs:ConnectionString"]
            ?? throw new InvalidOperationException("Acs:ConnectionString is not configured."));

    private string Sender =>
        config["Acs:SenderAddress"] ?? "donotreply@smilrhq.dk";

    public async Task SendVerificationEmailAsync(string to, string verifyUrl, CancellationToken ct = default)
    {
        var message = new EmailMessage(
            senderAddress: Sender,
            recipientAddress: to,
            content: new EmailContent("Verify your SmilrApi account")
            {
                Html = $"<p>Click the link below to verify your email and activate your SmilrApi account:</p>" +
                       $"<p><a href=\"{verifyUrl}\">{verifyUrl}</a></p>" +
                       "<p>This link expires in 24 hours.</p>",
                PlainText = $"Verify your SmilrApi account:\n{verifyUrl}\n\nThis link expires in 24 hours."
            });

        var op = await Client.SendAsync(WaitUntil.Started, message, ct);
        logger.LogInformation("Verification email queued to {To}, operationId={Id}", to, op.Id);
    }

    public async Task SendMagicLinkEmailAsync(string to, string loginUrl, CancellationToken ct = default)
    {
        var message = new EmailMessage(
            senderAddress: Sender,
            recipientAddress: to,
            content: new EmailContent("Your SmilrApi login link")
            {
                Html = $"<p>Click the link below to sign in to your SmilrApi dashboard:</p>" +
                       $"<p><a href=\"{loginUrl}\">{loginUrl}</a></p>" +
                       "<p>This link expires in 15 minutes. If you did not request this, you can ignore this email.</p>",
                PlainText = $"Sign in to SmilrApi:\n{loginUrl}\n\nExpires in 15 minutes. Ignore if you did not request this."
            });

        var op = await Client.SendAsync(WaitUntil.Started, message, ct);
        logger.LogInformation("Magic link email queued to {To}, operationId={Id}", to, op.Id);
    }

    public async Task SendScoreAlertEmailAsync(string to, string establishmentName, int newScore, CancellationToken ct = default)
    {
        var scoreLabel = newScore switch
        {
            1 => "Elite (1 smiley)",
            2 => "Good (2 smileys)",
            3 => "Needs improvement (3 smileys)",
            4 => "Not approved (4 smileys)",
            _ => $"Score {newScore}"
        };

        var message = new EmailMessage(
            senderAddress: Sender,
            recipientAddress: to,
            content: new EmailContent($"Smilr score update: {establishmentName}")
            {
                Html = $"<p>The Smilr score for <strong>{establishmentName}</strong> has changed.</p>" +
                       $"<p>New score: <strong>{scoreLabel}</strong></p>" +
                       "<p>Log in to your <a href=\"https://smilrhq.dk/dashboard.html\">Smilr dashboard</a> for details.</p>",
                PlainText = $"Smilr score update for {establishmentName}\nNew score: {scoreLabel}"
            });

        var op = await Client.SendAsync(WaitUntil.Started, message, ct);
        logger.LogInformation("Score alert email queued to {To} for '{Name}', operationId={Id}", to, establishmentName, op.Id);
    }
}
