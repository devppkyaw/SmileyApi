using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
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
}
