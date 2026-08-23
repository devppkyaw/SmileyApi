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
    /// an area-slug) — feeds the /find/{area-slug}/ hub page. <paramref name="sort"/> is one of
    /// FindEndpoints.ValidSortValues ("score_asc"/"score_desc"/"recent") or null for the default
    /// alphabetical-by-name order; unscored establishments always sort last regardless of direction.
    /// <paramref name="hideUnscored"/> excludes establishments with no LatestScore when true.</summary>
    Task<IReadOnlyList<Establishment>> GetByCitiesAsync(
        IReadOnlyList<string> cityValues, int page, int limit,
        string? sort = null, bool hideUnscored = false, CancellationToken ct = default);

    /// <summary>Total establishment count across the given raw City values, for hub-page pagination.
    /// <paramref name="hideUnscored"/> must match whatever was passed to GetByCitiesAsync for the count
    /// to agree with the page of results being paginated.</summary>
    Task<int> CountByCitiesAsync(IReadOnlyList<string> cityValues, bool hideUnscored = false, CancellationToken ct = default);

    /// <summary>Distinct raw Pixibranche values (with counts, most-common first) among establishments that
    /// have a CVR and a non-empty, non-placeholder Pixibranche — source data for the /find category-slug
    /// index. Unlike City, Pixibranche is a controlled vocabulary, so callers map 1:1 to a category-slug
    /// with no raw-spelling grouping needed.</summary>
    Task<IReadOnlyList<(string Category, int Count)>> GetCategoryCountsAsync(CancellationToken ct = default);

    /// <summary>Paginated establishments in the given raw City values AND the given raw Pixibranche value —
    /// feeds the /find/{area-slug}/{category-slug} hub page. Same sort/hideUnscored semantics as
    /// GetByCitiesAsync.</summary>
    Task<IReadOnlyList<Establishment>> GetByCitiesAndCategoryAsync(
        IReadOnlyList<string> cityValues, string category, int page, int limit,
        string? sort = null, bool hideUnscored = false, CancellationToken ct = default);

    /// <summary>Total establishment count for the given raw City values AND raw Pixibranche value — feeds
    /// hub-page pagination and the minimum-establishment-count indexing guard. <paramref name="hideUnscored"/>
    /// must match whatever was passed to GetByCitiesAndCategoryAsync for the count to agree with the page
    /// of results being paginated.</summary>
    Task<int> CountByCitiesAndCategoryAsync(IReadOnlyList<string> cityValues, string category, bool hideUnscored = false, CancellationToken ct = default);

    /// <summary>(City, Category, Count) triples across the whole dataset (CVR/City/Pixibranche all
    /// non-null, Pixibranche not a placeholder) — feeds the sitemap's area×category entries and the area
    /// hub page's "Browse by category" list without looping every category × every area as separate
    /// queries.</summary>
    Task<IReadOnlyList<(string City, string Category, int Count)>> GetCityCategoryCountsAsync(CancellationToken ct = default);

    /// <summary>Paginated establishments in the given raw City values with a recorded inspection date
    /// (LatestScoreDate not null), ordered by LatestScoreDate desc then Name asc as a deterministic
    /// tie-break — feeds the /find/{area-slug}/recently-inspected page.</summary>
    Task<IReadOnlyList<Establishment>> GetByCitiesOrderedByLatestInspectionAsync(
        IReadOnlyList<string> cityValues, int page, int limit, CancellationToken ct = default);

    /// <summary>Stats for the given raw City values feeding the /find/{area-slug}/recently-inspected
    /// page: how many establishments have a recorded inspection date (drives pagination and the
    /// indexing threshold), the most recent such date in the area, and how many of those establishments
    /// were inspected in the last 30 days.</summary>
    Task<RecentlyInspectedSummary> GetRecentlyInspectedSummaryAsync(
        IReadOnlyList<string> cityValues, CancellationToken ct = default);

    /// <summary>Establishments in the given raw City values whose current score transition (see
    /// ScoreChangeCalculator) falls on or after windowStart — one row per establishment (its most
    /// recent transition only), ordered by change date desc then Name asc — feeds
    /// /find/{area-slug}/changes.</summary>
    Task<IReadOnlyList<ScoreChangeRow>> GetRecentChangesByCitiesAsync(
        IReadOnlyList<string> cityValues, DateOnly windowStart, int page, int limit, CancellationToken ct = default);

    /// <summary>Stats for the given raw City values within the change window: total establishments
    /// with an in-window transition, how many improved vs downgraded, and the most recent change
    /// date.</summary>
    Task<ChangesSummary> GetChangesSummaryAsync(
        IReadOnlyList<string> cityValues, DateOnly windowStart, CancellationToken ct = default);

    /// <summary>(City, Count) of establishments with an in-window score transition, nationwide — feeds
    /// the /changes sitemap entries' per-area indexability check.</summary>
    Task<IReadOnlyList<(string City, int Count)>> GetChangeCountsByCityAsync(
        DateOnly windowStart, CancellationToken ct = default);

    /// <summary>Live score-distribution snapshot for the given raw City values — feeds the area hub
    /// page's "area health snapshot" stat ("X% currently have the top smiley"). Deliberately a separate
    /// query from GetChangesSummaryAsync: this answers "how healthy is this area's food scene right now",
    /// GetChangesSummaryAsync answers "what changed recently" — different questions, not interchangeable.</summary>
    Task<AreaScoreSnapshot> GetAreaScoreSnapshotAsync(IReadOnlyList<string> cityValues, CancellationToken ct = default);

    /// <summary>Same as GetAreaScoreSnapshotAsync, scoped down to one raw Pixibranche category within the
    /// given raw City values — feeds the category hub page's health snapshot. A parallel method rather
    /// than an optional category filter on GetAreaScoreSnapshotAsync, matching this repository's existing
    /// convention of parallel "...AndCategory" methods (GetByCitiesAsync/GetByCitiesAndCategoryAsync).</summary>
    Task<AreaScoreSnapshot> GetCategoryScoreSnapshotAsync(IReadOnlyList<string> cityValues, string category, CancellationToken ct = default);
}

public record SitemapEntry(string Name, string? City, int Navnelbnr, DateTime UpdatedAt, bool HasInspectionDate);

public record RecentlyInspectedSummary(int TotalWithInspection, DateOnly? LatestInspectionDate, int Last30DaysCount);

public record ScoreChangeRow(Establishment Establishment, int PreviousScore, int NewScore, DateOnly ChangeDate);

public record ChangesSummary(int TotalChanges, int ImprovedCount, int DowngradedCount, DateOnly? MostRecentChangeDate);

/// <summary>TopScoreCount / TotalScored of establishments currently holding the best possible score
/// (LatestScore == 1 — lower is better, per ScoreChangeCalculator's convention). TotalScored deliberately
/// excludes establishments with no recorded score yet ("kontrol på vej"), since including them would
/// dilute the percentage with businesses that simply haven't been inspected, misrepresenting how healthy
/// the area's already-inspected businesses actually are.</summary>
public record AreaScoreSnapshot(int TotalScored, int TopScoreCount)
{
    public double TopSharePercent => TotalScored == 0 ? 0 : Math.Round(100.0 * TopScoreCount / TotalScored, 1);
}
