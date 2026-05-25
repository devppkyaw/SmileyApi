using SmilrApi.Core.Models;

namespace SmilrApi.Core.Interfaces;

public interface IEstablishmentRepository
{
    Task<IReadOnlyList<Establishment>> GetByCvrAsync(string cvr, CancellationToken ct = default);
    Task<IReadOnlyList<Establishment>> SearchAsync(string query, int page, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Establishment>> GetNearbyAsync(double lat, double lng, double radiusKm, CancellationToken ct = default);
    Task<IReadOnlyList<Establishment>> GetHistoryAsync(string cvr, CancellationToken ct = default);
}
