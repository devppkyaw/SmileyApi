using Microsoft.Extensions.Logging;
using SmileyApi.Core.Interfaces;

namespace SmileyApi.Infrastructure.Services;

public class DevEmailService(ILogger<DevEmailService> logger) : IEmailService
{
    public Task SendVerificationEmailAsync(string to, string verifyUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Verification → {To} | {Url}", to, verifyUrl);
        return Task.CompletedTask;
    }

    public Task SendMagicLinkEmailAsync(string to, string loginUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Magic link → {To} | {Url}", to, loginUrl);
        return Task.CompletedTask;
    }

    public Task SendScoreAlertEmailAsync(string to, string establishmentName, int newScore, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Score alert → {To} | {Name} is now score {Score}", to, establishmentName, newScore);
        return Task.CompletedTask;
    }
}
