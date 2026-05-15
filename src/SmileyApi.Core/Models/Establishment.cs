namespace SmileyApi.Core.Models;

public class Establishment
{
    public int Id { get; set; }
    public int Navnelbnr { get; set; }
    public string? CvrNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? IndustryCode { get; set; }
    public string? IndustryName { get; set; }
    public double? GeoLat { get; set; }
    public double? GeoLng { get; set; }
    public string? ReportUrl { get; set; }
    public int? LatestScore { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
}
