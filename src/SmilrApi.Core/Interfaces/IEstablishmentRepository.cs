using SmilrApi.Core.Models;

namespace SmilrApi.Core.Interfaces;

public interface IEstablishmentRepository
{
    Task<IReadOnlyList<Establishment>> GetByCvrAsync(string cvr, CancellationToken ct = default);
    Task<IReadOnlyList<Establishment>> SearchAsync(string query, int page, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Establishment>> GetNearbyAsync(double lat, double lng, double radiusKm, CancellationToken ct = default);
    Task<IReadOnlyList<Establishment>> GetHistoryAsync(string cvr, CancellationToken ct = default);

    /// <summary>Single establishment by Navnelbnr (the actually-unique key) — used by the /find/{cvr}/{navnelbnr} detail page.</summary>
    Task<Establishment?> GetByNavnelbnrAsync(int navnelbnr, CancellationToken ct = default);

    /// <summary>Inspection history for one specific location (not merged across a whole CVR, unlike GetHistoryAsync).</summary>
    Task<Establishment?> GetHistoryByNavnelbnrAsync(int navnelbnr, CancellationToken ct = default);

    /// <summary>Lightweight (Cvr, Navnelbnr, UpdatedAt) projection for every establishment that has a CVR — feeds the /find sitemap.</summary>
    Task<IReadOnlyList<SitemapEntry>> GetAllForSitemapAsync(CancellationToken ct = default);
}

public record SitemapEntry(string CvrNumber, int Navnelbnr, DateTime UpdatedAt);
