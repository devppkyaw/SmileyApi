using SmileyApi.Core.Models;

namespace SmileyApi.Core.Interfaces;

public interface IApiKeyService
{
    Task<ApiKey?> ValidateAsync(string rawKey, CancellationToken ct = default);
    Task<string> GenerateAsync(string ownerEmail, string tier = "free", CancellationToken ct = default);
}
