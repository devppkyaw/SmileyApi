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
            "recent"     => query.OrderBy(e => e.LatestScoreDate == null ? 1 : 0).ThenByDescending(e => e.LatestScoreDate).ThenBy(e => e.Name),
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
        IReadOnlyList<string> cityValues, string category, int page, int limit, CancellationToken ct = default)
    {
        if (cityValues.Count == 0 || string.IsNullOrWhiteSpace(category)) return [];
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        return await db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City)
                     && e.Pixibranche == category)
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn).Take(1))
            .OrderBy(e => e.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<int> CountByCitiesAndCategoryAsync(
        IReadOnlyList<string> cityValues, string category, CancellationToken ct = default)
    {
        if (cityValues.Count == 0 || string.IsNullOrWhiteSpace(category)) return 0;
        return await db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City)
                     && e.Pixibranche == category)
            .CountAsync(ct);
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

        return new RecentlyInspectedSummary(
            await q.CountAsync(ct),
            await q.MaxAsync(e => (DateOnly?)e.LatestScoreDate, ct),
            await q.CountAsync(e => e.LatestScoreDate >= cutoff, ct));
    }

    public async Task<IReadOnlyList<ScoreChangeRow>> GetRecentChangesByCitiesAsync(
        IReadOnlyList<string> cityValues, DateOnly windowStart, int page, int limit, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return [];
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var establishments = await db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City))
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn))
            .AsNoTracking()
            .ToListAsync(ct);

        return establishments
            .Select(e => (Establishment: e, Change: ScoreChangeCalculator.LatestChange(e.Inspections)))
            .Where(x => x.Change is { } c && c.ChangeDate >= windowStart)
            .Select(x => new ScoreChangeRow(x.Establishment, x.Change!.Value.PreviousScore, x.Change.Value.NewScore, x.Change.Value.ChangeDate))
            .OrderByDescending(r => r.ChangeDate)
            .ThenBy(r => r.Establishment.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();
    }

    public async Task<ChangesSummary> GetChangesSummaryAsync(
        IReadOnlyList<string> cityValues, DateOnly windowStart, CancellationToken ct = default)
    {
        if (cityValues.Count == 0) return new ChangesSummary(0, 0, 0, null);

        var establishments = await db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && cityValues.Contains(e.City))
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn))
            .AsNoTracking()
            .ToListAsync(ct);

        var inWindow = establishments
            .Select(e => ScoreChangeCalculator.LatestChange(e.Inspections))
            .Where(c => c is { } cc && cc.ChangeDate >= windowStart)
            .Select(c => c!.Value)
            .ToList();

        return new ChangesSummary(
            inWindow.Count,
            inWindow.Count(c => c.NewScore < c.PreviousScore),
            inWindow.Count(c => c.NewScore > c.PreviousScore),
            inWindow.Count > 0 ? inWindow.Max(c => c.ChangeDate) : null);
    }

    public async Task<IReadOnlyList<(string City, int Count)>> GetChangeCountsByCityAsync(
        DateOnly windowStart, CancellationToken ct = default)
    {
        var establishments = await db.Establishments
            .Where(e => e.CvrNumber != null && e.City != null && e.City != "")
            .Include(e => e.Inspections.OrderByDescending(i => i.InspectedOn))
            .AsNoTracking()
            .ToListAsync(ct);

        return establishments
            .Select(e => (e.City, Change: ScoreChangeCalculator.LatestChange(e.Inspections)))
            .Where(x => x.Change is { } c && c.ChangeDate >= windowStart)
            .GroupBy(x => x.City!)
            .Select(g => (City: g.Key, Count: g.Count()))
            .ToList();
    }
}
