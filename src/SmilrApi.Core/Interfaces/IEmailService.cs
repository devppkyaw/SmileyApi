using SmilrApi.Core.Models;

namespace SmilrApi.Core.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string to, string verifyUrl, CancellationToken ct = default);
    Task SendMagicLinkEmailAsync(string to, string loginUrl, CancellationToken ct = default);
    Task SendScoreAlertEmailAsync(string to, IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default);
    Task SendSystemScoreDigestAsync(IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default);
}
