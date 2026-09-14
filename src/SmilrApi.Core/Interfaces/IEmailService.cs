using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;

namespace SmilrApi.Core.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string to, string companyName, string verifyUrl, CancellationToken ct = default);
    Task SendMagicLinkEmailAsync(string to, string companyName, string loginUrl, CancellationToken ct = default);
    Task SendScoreAlertEmailAsync(string to, IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default);
    Task SendSystemScoreDigestAsync(IReadOnlyList<ScoreAlertItem> changes, CancellationToken ct = default);

    Task SendFeedHealthAlertAsync(
        FeedHealthAlertKind kind,
        string? fieldName,
        FeedHealthAlertStatus status,
        string summary,
        DateTime? firstDetectedAt,
        CancellationToken ct = default);
}
