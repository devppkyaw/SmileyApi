using SmilrApi.Core.Models;

namespace SmilrApi.Core.Interfaces;

public interface IApiKeyService
{
    Task<ApiKey?> ValidateAsync(string rawKey, CancellationToken ct = default);
    Task<(string Plaintext, ApiKey Key)> GenerateAsync(string ownerEmail, string tier = "free", CancellationToken ct = default);
}
