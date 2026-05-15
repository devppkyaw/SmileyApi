using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmileyApi.Infrastructure.Data;
using System.Data;

namespace SmileyApi.Infrastructure.Services;

public record SyncRow(
    int Navnelbnr,
    string? CvrNumber,
    string Name,
    string? Address,
    string? PostalCode,
    string? City,
    string? IndustryCode,
    string? IndustryName,
    double? GeoLat,
    double? GeoLng,
    string? ReportUrl,
    List<(int Score, DateOnly Date)> Inspections
);

public class EstablishmentSyncService(SmileyDbContext db, ILogger<EstablishmentSyncService> logger)
{
    public async Task SyncAsync(IReadOnlyList<SyncRow> rows, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var conn = (SqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        // ── Establishments ───────────────────────────────────────────────
        await ExecAsync(conn, @"
            DROP TABLE IF EXISTS #estab_staging;
            CREATE TABLE #estab_staging (
                Navnelbnr   INT            NOT NULL,
                CvrNumber   NVARCHAR(20)   NULL,
                Name        NVARCHAR(512)  NOT NULL,
                Address     NVARCHAR(512)  NULL,
                PostalCode  NVARCHAR(10)   NULL,
                City        NVARCHAR(256)  NULL,
                IndustryCode NVARCHAR(32)  NULL,
                IndustryName NVARCHAR(256) NULL,
                GeoLat      FLOAT          NULL,
                GeoLng      FLOAT          NULL,
                ReportUrl   NVARCHAR(1024) NULL,
                LatestScore INT            NULL
            );", ct);

        await BulkCopyAsync(conn, "#estab_staging", BuildEstabDataTable(rows), ct);

        int inserted = 0, updated = 0;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandTimeout = 300;
            cmd.CommandText = @"
                DECLARE @out TABLE (action NVARCHAR(10));
                MERGE Establishments WITH (HOLDLOCK) AS T
                USING #estab_staging AS S ON T.Navnelbnr = S.Navnelbnr
                WHEN MATCHED AND (
                    T.Name                               <> S.Name OR
                    ISNULL(T.CvrNumber,   '')            <> ISNULL(S.CvrNumber,   '') OR
                    ISNULL(T.Address,     '')            <> ISNULL(S.Address,     '') OR
                    ISNULL(T.PostalCode,  '')            <> ISNULL(S.PostalCode,  '') OR
                    ISNULL(T.City,        '')            <> ISNULL(S.City,        '') OR
                    ISNULL(T.IndustryCode,'')            <> ISNULL(S.IndustryCode,'') OR
                    ISNULL(T.IndustryName,'')            <> ISNULL(S.IndustryName,'') OR
                    ISNULL(CAST(T.GeoLat AS NVARCHAR(40)),'') <> ISNULL(CAST(S.GeoLat AS NVARCHAR(40)),'') OR
                    ISNULL(CAST(T.GeoLng AS NVARCHAR(40)),'') <> ISNULL(CAST(S.GeoLng AS NVARCHAR(40)),'') OR
                    ISNULL(T.ReportUrl,   '')            <> ISNULL(S.ReportUrl,   '')
                ) THEN UPDATE SET
                    T.CvrNumber    = S.CvrNumber,
                    T.Name         = S.Name,
                    T.Address      = S.Address,
                    T.PostalCode   = S.PostalCode,
                    T.City         = S.City,
                    T.IndustryCode = S.IndustryCode,
                    T.IndustryName = S.IndustryName,
                    T.GeoLat       = S.GeoLat,
                    T.GeoLng       = S.GeoLng,
                    T.ReportUrl    = S.ReportUrl,
                    T.LatestScore  = S.LatestScore,
                    T.UpdatedAt    = GETUTCDATE()
                WHEN NOT MATCHED THEN INSERT (
                    Navnelbnr, CvrNumber, Name, Address, PostalCode, City,
                    IndustryCode, IndustryName, GeoLat, GeoLng, ReportUrl,
                    LatestScore, FirstSeenAt, UpdatedAt
                ) VALUES (
                    S.Navnelbnr, S.CvrNumber, S.Name, S.Address, S.PostalCode, S.City,
                    S.IndustryCode, S.IndustryName, S.GeoLat, S.GeoLng, S.ReportUrl,
                    S.LatestScore, GETUTCDATE(), GETUTCDATE()
                )
                OUTPUT $action INTO @out;
                SELECT action, COUNT(*) FROM @out GROUP BY action;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var action = reader.GetString(0);
                var count = reader.GetInt32(1);
                if (action == "INSERT") inserted = count;
                else if (action == "UPDATE") updated = count;
            }
        }

        // ── Inspections ──────────────────────────────────────────────────
        var navToId = await db.Establishments.AsNoTracking()
            .Select(e => new { e.Navnelbnr, e.Id })
            .ToDictionaryAsync(e => e.Navnelbnr, e => e.Id, ct);

        var (inspDt, inspCount) = BuildInspectionDataTable(rows, navToId, now);

        if (inspCount > 0)
        {
            await ExecAsync(conn, @"
                DROP TABLE IF EXISTS #inspection_staging;
                CREATE TABLE #inspection_staging (
                    EstablishmentId INT       NOT NULL,
                    SmileyScore     INT       NOT NULL,
                    InspectedOn     DATE      NOT NULL,
                    RecordedAt      DATETIME2 NOT NULL
                );", ct);

            await BulkCopyAsync(conn, "#inspection_staging", inspDt, ct);

            await ExecAsync(conn, @"
                MERGE Inspections WITH (HOLDLOCK) AS T
                USING (
                    SELECT EstablishmentId, SmileyScore, InspectedOn, RecordedAt
                    FROM (
                        SELECT *, ROW_NUMBER() OVER (
                            PARTITION BY EstablishmentId, InspectedOn
                            ORDER BY (SELECT NULL)
                        ) AS rn
                        FROM #inspection_staging
                    ) AS x
                    WHERE rn = 1
                ) AS S ON T.EstablishmentId = S.EstablishmentId AND T.InspectedOn = S.InspectedOn
                WHEN NOT MATCHED THEN INSERT (
                    EstablishmentId, SmileyScore, InspectedOn, RecordedAt
                ) VALUES (
                    S.EstablishmentId, S.SmileyScore, S.InspectedOn, S.RecordedAt
                );", ct, timeout: 300);
        }

        logger.LogInformation(
            "Sync complete: {New} new, {Updated} updated, {Unchanged} unchanged; {Inspections} inspection rows staged.",
            inserted, updated, rows.Count - inserted - updated, inspCount);
    }

    private static DataTable BuildEstabDataTable(IReadOnlyList<SyncRow> rows)
    {
        var dt = new DataTable();
        dt.Columns.Add("Navnelbnr", typeof(int));
        dt.Columns.Add("CvrNumber", typeof(string));
        dt.Columns.Add("Name", typeof(string));
        dt.Columns.Add("Address", typeof(string));
        dt.Columns.Add("PostalCode", typeof(string));
        dt.Columns.Add("City", typeof(string));
        dt.Columns.Add("IndustryCode", typeof(string));
        dt.Columns.Add("IndustryName", typeof(string));
        dt.Columns.Add("GeoLat", typeof(double));
        dt.Columns.Add("GeoLng", typeof(double));
        dt.Columns.Add("ReportUrl", typeof(string));
        dt.Columns.Add("LatestScore", typeof(int));

        foreach (var row in rows)
        {
            dt.Rows.Add(
                row.Navnelbnr,
                NullOr(row.CvrNumber),
                row.Name,
                NullOr(row.Address),
                NullOr(row.PostalCode),
                NullOr(row.City),
                NullOr(row.IndustryCode),
                NullOr(row.IndustryName),
                (object?)row.GeoLat ?? DBNull.Value,
                (object?)row.GeoLng ?? DBNull.Value,
                NullOr(row.ReportUrl),
                row.Inspections.Count > 0 ? (object)row.Inspections[0].Score : DBNull.Value
            );
        }

        return dt;
    }

    private static (DataTable dt, int count) BuildInspectionDataTable(
        IReadOnlyList<SyncRow> rows,
        Dictionary<int, int> navToId,
        DateTime now)
    {
        var dt = new DataTable();
        dt.Columns.Add("EstablishmentId", typeof(int));
        dt.Columns.Add("SmileyScore", typeof(int));
        dt.Columns.Add("InspectedOn", typeof(DateTime));
        dt.Columns.Add("RecordedAt", typeof(DateTime));

        int count = 0;
        foreach (var row in rows)
        {
            if (!navToId.TryGetValue(row.Navnelbnr, out var estId))
                continue;

            foreach (var (score, date) in row.Inspections)
            {
                dt.Rows.Add(estId, score, date.ToDateTime(TimeOnly.MinValue), now);
                count++;
            }
        }

        return (dt, count);
    }

    private static async Task BulkCopyAsync(SqlConnection conn, string table, DataTable dt, CancellationToken ct)
    {
        using var bcp = new SqlBulkCopy(conn) { DestinationTableName = table, BulkCopyTimeout = 300 };
        foreach (DataColumn col in dt.Columns)
            bcp.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        await bcp.WriteToServerAsync(dt, ct);
    }

    private static async Task ExecAsync(SqlConnection conn, string sql, CancellationToken ct, int timeout = 60)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = timeout;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static object NullOr(string? value) => (object?)value ?? DBNull.Value;
}
