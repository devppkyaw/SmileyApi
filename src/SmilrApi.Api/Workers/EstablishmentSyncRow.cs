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
    List<(int Score, DateOnly Date)> Inspections
);
