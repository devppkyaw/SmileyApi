using SmilrApi.Core.Models;

namespace SmilrApi.Core.Interfaces;

public interface IBusinessService
{
    Task<Business?> RegisterOrResendAsync(string email, string companyName, bool marketingConsent, string baseUrl, CancellationToken ct = default);
    Task<Business?> VerifyEmailAsync(string token, CancellationToken ct = default);
    Task<bool> RequestMagicLinkAsync(string email, string baseUrl, CancellationToken ct = default);
    Task<Business?> VerifyMagicLinkAsync(string token, CancellationToken ct = default);
    Task<Business?> GetByIdAsync(int id, CancellationToken ct = default);
}
