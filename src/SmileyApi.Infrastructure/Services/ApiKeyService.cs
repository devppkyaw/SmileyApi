using SmileyApi.Core.Interfaces;
using SmileyApi.Core.Models;
using SmileyApi.Infrastructure.Data;

namespace SmileyApi.Infrastructure.Services;

public class ApiKeyService(SmileyDbContext db) : IApiKeyService
{
    public Task<ApiKey?> ValidateAsync(string rawKey, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<string> GenerateAsync(string ownerEmail, string tier = "free", CancellationToken ct = default) =>
        throw new NotImplementedException();
}
