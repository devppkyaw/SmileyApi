namespace SmilrApi.Core.Utils;

/// <summary>Normalizes the dashboard Locations list's paging/sort query params so the endpoint never
/// trusts raw client values (page 0, pageSize 10,000, unknown sort keys).</summary>
public static class LocationListPaging
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public const string SortByCvr = "cvr";
    public const string SortScoreAsc = "score-asc";
    public const string SortScoreDesc = "score-desc";
    /// <summary>Most concerning first, per <see cref="RiskCalculator"/>'s rank.</summary>
    public const string SortRisk = "risk";

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));

    public static string NormalizeSort(string? sort) => sort switch
    {
        SortScoreAsc  => SortScoreAsc,
        SortScoreDesc => SortScoreDesc,
        SortRisk      => SortRisk,
        _             => SortByCvr,
    };

    /// <summary>Trims the search text and caps its length; null when nothing searchable remains.</summary>
    public static string? NormalizeSearch(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return null;
        var trimmed = q.Trim();
        return trimmed.Length > 100 ? trimmed[..100] : trimmed;
    }
}
