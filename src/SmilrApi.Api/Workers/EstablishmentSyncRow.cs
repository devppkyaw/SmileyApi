namespace SmilrApi.Api.Workers;

public record EstablishmentSyncRow(
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
    string? VirksomhedsType,
    string? Pixibranche,
    DateOnly? LatestScoreDate,
    string? PNumber,
    List<(int Score, DateOnly Date)> Inspections
);
