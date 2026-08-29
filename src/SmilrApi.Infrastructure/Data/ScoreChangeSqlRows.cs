namespace SmilrApi.Infrastructure.Data;

// Keyless DTOs used only as FromSqlRaw result shapes for the score-transition queries in
// EstablishmentRepository (GetRecentChangesByCitiesAsync/GetChangesSummaryAsync/
// GetChangeCountsByCityAsync) — never a real table/view, registered with HasNoKey()+ToView(null)
// in SmilrDbContext so they're excluded from migrations.

internal sealed class ScoreChangeSqlRow
{
    public int EstablishmentId { get; set; }
    public int PreviousScore { get; set; }
    public int NewScore { get; set; }
    public DateOnly ChangeDate { get; set; }
}

internal sealed class CityChangeCountRow
{
    public string City { get; set; } = string.Empty;
    public int Count { get; set; }
}

internal sealed class ChangesSummaryRow
{
    public int TotalChanges { get; set; }
    public int ImprovedCount { get; set; }
    public int DowngradedCount { get; set; }
    public DateOnly? MostRecentChangeDate { get; set; }
}
