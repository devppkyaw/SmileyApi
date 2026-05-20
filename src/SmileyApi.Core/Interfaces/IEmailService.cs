namespace SmileyApi.Core.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string to, string verifyUrl, CancellationToken ct = default);
    Task SendMagicLinkEmailAsync(string to, string loginUrl, CancellationToken ct = default);
    Task SendScoreAlertEmailAsync(string to, string establishmentName, int newScore, CancellationToken ct = default);
}
