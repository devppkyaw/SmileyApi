using SmileyApi.Core.Interfaces;
using SmileyApi.Core.Models;
using SmileyApi.Infrastructure.Data;

namespace SmileyApi.Infrastructure.Repositories;

public class EstablishmentRepository(SmileyDbContext db) : IEstablishmentRepository
{
    public Task<Establishment?> GetByCvrAsync(string cvr, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Establishment>> SearchAsync(string query, int page, int limit, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Establishment>> GetNearbyAsync(double lat, double lng, double radiusKm, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Inspection>> GetHistoryAsync(string cvr, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
