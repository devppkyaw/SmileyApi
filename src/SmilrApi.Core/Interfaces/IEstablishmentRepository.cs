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

    /// <summary>Lightweight (Name, City, Navnelbnr, UpdatedAt) projection for every establishment that has a CVR — feeds the /find sitemap.</summary>
    Task<IReadOnlyList<SitemapEntry>> GetAllForSitemapAsync(CancellationToken ct = default);

    /// <summary>Distinct raw City values (with counts, most-common first) among establishments that have a
    /// CVR and a non-empty City — source data for building the /find/{area-slug}/ lookup and the sitemap's
    /// hub-page entries. Grouping by area-slug (Slugifier.Slugify(City)) happens in the caller, since raw
    /// spellings aren't normalized in the DB (e.g. "København", "KØBENHAVN", "Kobenhavn " all collapse to
    /// the same area-slug).</summary>
    Task<IReadOnlyList<(string City, int Count)>> GetCityCountsAsync(CancellationToken ct = default);

    /// <summary>Paginated establishments whose City is one of the given raw values (already resolved from
    /// an area-slug) — feeds the /find/{area-slug}/ hub page.</summary>
    Task<IReadOnlyList<Establishment>> GetByCitiesAsync(IReadOnlyList<string> cityValues, int page, int limit, CancellationToken ct = default);

    /// <summary>Total establishment count across the given raw City values, for hub-page pagination.</summary>
    Task<int> CountByCitiesAsync(IReadOnlyList<string> cityValues, CancellationToken ct = default);

    /// <summary>Distinct raw Pixibranche values (with counts, most-common first) among establishments that
    /// have a CVR and a non-empty, non-placeholder Pixibranche — source data for the /find category-slug
    /// index. Unlike City, Pixibranche is a controlled vocabulary, so callers map 1:1 to a category-slug
    /// with no raw-spelling grouping needed.</summary>
    Task<IReadOnlyList<(string Category, int Count)>> GetCategoryCountsAsync(CancellationToken ct = default);

    /// <summary>Paginated establishments in the given raw City values AND the given raw Pixibranche value —
    /// feeds the /find/{area-slug}/{category-slug} hub page.</summary>
    Task<IReadOnlyList<Establishment>> GetByCitiesAndCategoryAsync(IReadOnlyList<string> cityValues, string category, int page, int limit, CancellationToken ct = default);

    /// <summary>Total establishment count for the given raw City values AND raw Pixibranche value — feeds
    /// hub-page pagination and the minimum-establishment-count indexing guard.</summary>
    Task<int> CountByCitiesAndCategoryAsync(IReadOnlyList<string> cityValues, string category, CancellationToken ct = default);

    /// <summary>(City, Category, Count) triples across the whole dataset (CVR/City/Pixibranche all
    /// non-null, Pixibranche not a placeholder) — feeds the sitemap's area×category entries and the area
    /// hub page's "Browse by category" list without looping every category × every area as separate
    /// queries.</summary>
    Task<IReadOnlyList<(string City, string Category, int Count)>> GetCityCategoryCountsAsync(CancellationToken ct = default);
}

public record SitemapEntry(string Name, string? City, int Navnelbnr, DateTime UpdatedAt);
