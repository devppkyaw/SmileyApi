using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;
using SmilrApi.Infrastructure.Data;

namespace SmilrApi.Infrastructure.Repositories;

public class EstablishmentRepository(SmilrDbContext db) : IEstablishmentRepository
{
    public async Task<IReadOnlyList<Establishment>> GetByCvrAsync(string cvr, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cvr)) return [];
        return await db.Establishments
            .Where(e => e.CvrNumber == cvr)
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn).Take(1))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Establishment>> SearchAsync(
        string query, int page, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var pattern = "%" + query.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";

        return await db.Establishments
            .Where(e =>
                EF.Functions.Like(e.Name, pattern) ||
                EF.Functions.Like(e.Address ?? "", pattern) ||
                EF.Functions.Like(e.City ?? "", pattern))
            .OrderBy(e => e.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Establishment>> GetNearbyAsync(
        double lat, double lng, double radiusKm, CancellationToken ct = default)
    {
        if (radiusKm <= 0) return [];

        const string sql = """
            SELECT TOP (50) e.*
            FROM Establishments e
            WHERE
                e.GeoLat IS NOT NULL AND e.GeoLng IS NOT NULL
                AND e.GeoLat BETWEEN @lat - (@radius / 111.0) AND @lat + (@radius / 111.0)
                AND e.GeoLng BETWEEN @lng - (@radius / (111.0 * COS(RADIANS(@lat))))
                             AND @lng + (@radius / (111.0 * COS(RADIANS(@lat))))
                AND (6371.0 * 2 * ASIN(SQRT(
                        POWER(SIN(RADIANS((e.GeoLat - @lat) / 2.0)), 2) +
                        COS(RADIANS(@lat)) * COS(RADIANS(e.GeoLat)) *
                        POWER(SIN(RADIANS((e.GeoLng - @lng) / 2.0)), 2)
                    ))) <= @radius
            ORDER BY (6371.0 * 2 * ASIN(SQRT(
                POWER(SIN(RADIANS((e.GeoLat - @lat) / 2.0)), 2) +
                COS(RADIANS(@lat)) * COS(RADIANS(e.GeoLat)) *
                POWER(SIN(RADIANS((e.GeoLng - @lng) / 2.0)), 2)
            ))) ASC
            """;

        return await db.Establishments
            .FromSqlRaw(sql,
                new SqlParameter("@lat",    lat),
                new SqlParameter("@lng",    lng),
                new SqlParameter("@radius", radiusKm))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Establishment>> GetHistoryAsync(
        string cvr, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cvr)) return [];
        return await db.Establishments
            .Where(e => e.CvrNumber == cvr)
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Establishment?> GetByNavnelbnrAsync(int navnelbnr, CancellationToken ct = default)
    {
        return await db.Establishments
            .Where(e => e.Navnelbnr == navnelbnr)
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn).Take(1))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Establishment?> GetHistoryByNavnelbnrAsync(int navnelbnr, CancellationToken ct = default)
    {
        return await db.Establishments
            .Where(e => e.Navnelbnr == navnelbnr)
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<SitemapEntry>> GetAllForSitemapAsync(CancellationToken ct = default)
    {
        return await db.Establishments
            .Where(e => e.CvrNumber != null)
            .Select(e => new SitemapEntry(e.Name, e.City, e.Navnelbnr, e.UpdatedAt, e.LatestScoreDate != null))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(string City, int Count)>> GetCityCountsAsync(CancellationToken ct = default)
    {
        var rows = await db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && e.City != "")
            .GroupBy(e => e.City)
            .Select(g => new { City = g.Key!, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(r => (r.City, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<Establishment>> GetByCitiesAsync(
        IReadOnlyList<string> cityValues, int page, int limit,
        string? sort = null, bool hideUnscored = false, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return [];
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var query = db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City));

        if (hideUnscored) query = query.Where(e => e.LatestScore != null);

        // Unscored establishments (LatestScore/LatestScoreDate null) always sort last regardless of
        // direction — the `== null ? 1 : 0` primary key pushes them to the end before the real ordering
        // takes over. Name is always the final tie-break so paging stays stable page to page.
        query = sort switch
        {
            "score_asc"  => query.OrderBy(e => e.LatestScore == null ? 1 : 0).ThenBy(e => e.LatestScore).ThenBy(e => e.Name),
            "score_desc" => query.OrderBy(e => e.LatestScore == null ? 1 : 0).ThenByDescending(e => e.LatestScore).ThenBy(e => e.Name),
            _            => query.OrderBy(e => e.Name)
        };

        return await query
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn).Take(1))
            .Skip((page - 1) * limit)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<int> CountByCitiesAsync(IReadOnlyList<string> cityValues, bool hideUnscored = false, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return 0;
        var query = db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City));
        if (hideUnscored) query = query.Where(e => e.LatestScore != null);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<(string Category, int Count)>> GetCategoryCountsAsync(CancellationToken ct = default)
    {
        var rows = await db.Establishments
            .Where(e => e.CvrNumber != null && e.Pixibranche != null && e.Pixibranche != ""
                     && !PixibrancheCategories.Placeholders.Contains(e.Pixibranche))
            .GroupBy(e => e.Pixibranche)
            .Select(g => new { Category = g.Key!, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(r => (r.Category, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<Establishment>> GetByCitiesAndCategoryAsync(
        IReadOnlyList<string> cityValues, string category, int page, int limit,
        string? sort = null, bool hideUnscored = false, CancellationToken ct = default)
    {
        if (cityValues.Count == 0 || string.IsNullOrWhiteSpace(category)) return [];
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var query = db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City)
                     && e.Pixibranche == category);

        if (hideUnscored) query = query.Where(e => e.LatestScore != null);

        // Same sort shape (and the same "unscored always last" behavior) as GetByCitiesAsync.
        query = sort switch
        {
            "score_asc"  => query.OrderBy(e => e.LatestScore == null ? 1 : 0).ThenBy(e => e.LatestScore).ThenBy(e => e.Name),
            "score_desc" => query.OrderBy(e => e.LatestScore == null ? 1 : 0).ThenByDescending(e => e.LatestScore).ThenBy(e => e.Name),
            _            => query.OrderBy(e => e.Name)
        };

        return await query
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn).Take(1))
            .Skip((page - 1) * limit)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<int> CountByCitiesAndCategoryAsync(
        IReadOnlyList<string> cityValues, string category, bool hideUnscored = false, CancellationToken ct = default)
    {
        if (cityValues.Count == 0 || string.IsNullOrWhiteSpace(category)) return 0;
        var query = db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City)
                     && e.Pixibranche == category);
        if (hideUnscored) query = query.Where(e => e.LatestScore != null);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<(string City, string Category, int Count)>> GetCityCategoryCountsAsync(CancellationToken ct = default)
    {
        var rows = await db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && e.City != ""
                     && e.Pixibranche != null && e.Pixibranche != ""
                     && !PixibrancheCategories.Placeholders.Contains(e.Pixibranche))
            .GroupBy(e => new { e.City, e.Pixibranche })
            .Select(g => new { g.Key.City, g.Key.Pixibranche, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(r => (r.City!, r.Pixibranche!, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<Establishment>> GetByCitiesOrderedByLatestInspectionAsync(
        IReadOnlyList<string> cityValues, int page, int limit, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return [];
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        return await db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City)
                     && e.LatestScoreDate != null)
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn).Take(1))
            .OrderByDescending(e => e.LatestScoreDate)
            .ThenBy(e => e.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<RecentlyInspectedSummary> GetRecentlyInspectedSummaryAsync(
        IReadOnlyList<string> cityValues, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return new RecentlyInspectedSummary(0, null, 0);
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var q = db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City)
                     && e.LatestScoreDate != null);

        // Single conditional-aggregation query instead of 3 sequential round trips — this call
        // stays live on every request (it's RecentlyInspectedHandlerAsync's 404 gate), so shrinking
        // it directly shrinks the page's worst-case latency, not just the cache-miss path.
        var result = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                MaxDate = g.Max(e => (DateOnly?)e.LatestScoreDate),
                Recent = g.Count(e => e.LatestScoreDate >= cutoff)
            })
            .SingleOrDefaultAsync(ct);
        return result is null
            ? new RecentlyInspectedSummary(0, null, 0)
            : new RecentlyInspectedSummary(result.Total, result.MaxDate, result.Recent);
    }

    // establishmentFilterSql is appended verbatim after "WHERE e.CvrNumber IS NOT NULL" — either the
    // nationwide non-empty-City filter, or a parameterized "AND e.City IN (...)" from
    // BuildCityInClause. Never raw, unparameterized city text.
    //
    // Pushes ScoreChangeCalculator.LatestChange's "walk newest->oldest, return the first score
    // divergence" semantics into SQL: LAG(SmileyScore) OVER (PARTITION BY EstablishmentId ORDER BY
    // InspectedOn) (chronological) marks every row whose score differs from its immediate
    // predecessor as a transition point; ROW_NUMBER() OVER (PARTITION BY EstablishmentId ORDER BY
    // ChangeDate DESC) = 1 among transition points only then picks the most recent one — equivalent
    // to LatestChange's backward walk, not merely "compare the two newest rows" (a same-score
    // re-inspection after a real change does not erase that change, in either formulation).
    private static string BuildCurrentTransitionsCte(string establishmentFilterSql) => $"""
        WITH Deltas AS (
            SELECT
                i.EstablishmentId,
                i.SmileyScore AS NewScore,
                LAG(i.SmileyScore) OVER (PARTITION BY i.EstablishmentId ORDER BY i.InspectedOn) AS PreviousScore,
                i.InspectedOn AS ChangeDate
            FROM Inspections i
            INNER JOIN Establishments e ON e.Id = i.EstablishmentId
            WHERE e.CvrNumber IS NOT NULL {establishmentFilterSql}
        ),
        TransitionPoints AS (
            SELECT EstablishmentId, PreviousScore, NewScore, ChangeDate
            FROM Deltas
            WHERE PreviousScore IS NOT NULL AND PreviousScore <> NewScore
        ),
        CurrentTransitions AS (
            SELECT EstablishmentId, PreviousScore, NewScore, ChangeDate,
                   ROW_NUMBER() OVER (PARTITION BY EstablishmentId ORDER BY ChangeDate DESC) AS rn
            FROM TransitionPoints
        )
        """;

    // City values travel as SqlParameters, never concatenated into SQL text — only placeholder names
    // (@city0, @city1, ...) go into the string. FromSqlInterpolated can't parameterize a
    // variable-length list the way EF's .Contains() LINQ translation does, hence FromSqlRaw +
    // explicit SqlParameter[] (the same idiom GetNearbyAsync above already uses).
    private static (string Sql, List<SqlParameter> Parameters) BuildCityInClause(IReadOnlyList<string> cityValues)
    {
        var names = new List<string>(cityValues.Count);
        var parameters = new List<SqlParameter>(cityValues.Count);
        for (var i = 0; i < cityValues.Count; i++)
        {
            var name = $"@city{i}";
            names.Add(name);
            parameters.Add(new SqlParameter(name, cityValues[i]));
        }
        return ($"AND e.City IN ({string.Join(", ", names)})", parameters);
    }

    public async Task<IReadOnlyList<ScoreChangeRow>> GetRecentChangesByCitiesAsync(
        IReadOnlyList<string> cityValues, DateOnly windowStart, int page, int limit, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return [];
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var (cityFilterSql, cityParams) = BuildCityInClause(cityValues);
        var sql = BuildCurrentTransitionsCte(cityFilterSql) + """
            SELECT ct.EstablishmentId AS EstablishmentId, ct.PreviousScore AS PreviousScore,
                   ct.NewScore AS NewScore, ct.ChangeDate AS ChangeDate
            FROM CurrentTransitions ct
            INNER JOIN Establishments e ON e.Id = ct.EstablishmentId
            WHERE ct.rn = 1 AND ct.ChangeDate >= @windowStart
            ORDER BY ct.ChangeDate DESC, e.Name ASC
            OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
            """;

        var parameters = new List<SqlParameter>(cityParams)
        {
            new("@windowStart", windowStart),
            new("@offset", (page - 1) * limit),
            new("@limit", limit),
        };

        var rows = await db.Set<ScoreChangeSqlRow>()
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync(ct);

        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.EstablishmentId).ToList();
        var establishments = await db.Establishments
            .Where(e => ids.Contains(e.Id))
            .AsNoTracking()
            .ToDictionaryAsync(e => e.Id, ct);

        // Re-zip in the SQL-determined order — the follow-up lookup is O(1) per row, bounded to
        // `limit` (<=100) entries, so this doesn't reintroduce the original unbounded-materialization
        // cost this method used to have.
        return rows
            .Select(r => new ScoreChangeRow(establishments[r.EstablishmentId], r.PreviousScore, r.NewScore, r.ChangeDate))
            .ToList();
    }

    public async Task<ChangesSummary> GetChangesSummaryAsync(
        IReadOnlyList<string> cityValues, DateOnly windowStart, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return new ChangesSummary(0, 0, 0, null);

        var (cityFilterSql, cityParams) = BuildCityInClause(cityValues);
        var sql = BuildCurrentTransitionsCte(cityFilterSql) + """
            SELECT
                COUNT(*) AS TotalChanges,
                ISNULL(SUM(CASE WHEN ct.NewScore < ct.PreviousScore THEN 1 ELSE 0 END), 0) AS ImprovedCount,
                ISNULL(SUM(CASE WHEN ct.NewScore > ct.PreviousScore THEN 1 ELSE 0 END), 0) AS DowngradedCount,
                MAX(ct.ChangeDate) AS MostRecentChangeDate
            FROM CurrentTransitions ct
            WHERE ct.rn = 1 AND ct.ChangeDate >= @windowStart
            """;

        var parameters = new List<SqlParameter>(cityParams) { new("@windowStart", windowStart) };

        // No GROUP BY -> always exactly one row, even over zero matches (COUNT=0, SUM->NULL hence
        // ISNULL, MAX->NULL = the desired "no changes" sentinel). Materialize with ToListAsync and
        // take Single() client-side rather than SingleAsync directly on the FromSqlRaw query — EF
        // Core needs to compose extra SQL around a query operator like SingleAsync (e.g. to cap the
        // row count), and SQL Server rejects composing on top of raw SQL that contains a CTE.
        var row = (await db.Set<ChangesSummaryRow>()
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync(ct))
            .Single();

        return new ChangesSummary(row.TotalChanges, row.ImprovedCount, row.DowngradedCount, row.MostRecentChangeDate);
    }

    public async Task<IReadOnlyList<(string City, int Count)>> GetChangeCountsByCityAsync(
        DateOnly windowStart, CancellationToken ct = default)
    {
        var sql = BuildCurrentTransitionsCte("AND e.City IS NOT NULL AND e.City <> ''") + """
            SELECT e.City AS City, COUNT(*) AS Count
            FROM CurrentTransitions ct
            INNER JOIN Establishments e ON e.Id = ct.EstablishmentId
            WHERE ct.rn = 1 AND ct.ChangeDate >= @windowStart
            GROUP BY e.City
            """;

        var rows = await db.Set<CityChangeCountRow>()
            .FromSqlRaw(sql, new SqlParameter("@windowStart", windowStart))
            .ToListAsync(ct);

        return rows.Select(r => (r.City, r.Count)).ToList();
    }

    public async Task<AreaScoreSnapshot> GetAreaScoreSnapshotAsync(IReadOnlyList<string> cityValues, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return new AreaScoreSnapshot(0, 0);

        var scored = db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City) && e.LatestScore != null);

        // Single conditional-aggregation query instead of two sequential COUNTs — same result, half
        // the DB round trips. GroupBy(_ => 1) always yields at most one group, so SingleOrDefaultAsync
        // is safe (null only when `scored` itself is empty).
        var counts = await scored
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Top = g.Count(e => e.LatestScore == 1) })
            .SingleOrDefaultAsync(ct);
        return counts is null ? new AreaScoreSnapshot(0, 0) : new AreaScoreSnapshot(counts.Total, counts.Top);
    }

    public async Task<AreaScoreSnapshot> GetCategoryScoreSnapshotAsync(IReadOnlyList<string> cityValues, string category, CancellationToken ct = default)
    {
        if (cityValues.Count == 0 || string.IsNullOrWhiteSpace(category)) return new AreaScoreSnapshot(0, 0);

        var scored = db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City)
                     && e.Pixibranche == category && e.LatestScore != null);

        // Same single-query combination as GetAreaScoreSnapshotAsync above.
        var counts = await scored
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Top = g.Count(e => e.LatestScore == 1) })
            .SingleOrDefaultAsync(ct);
        return counts is null ? new AreaScoreSnapshot(0, 0) : new AreaScoreSnapshot(counts.Total, counts.Top);
    }
}
