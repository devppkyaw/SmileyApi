namespace SmileyApi.Core.Models;

public class Inspection
{
    public int Id { get; set; }
    public int EstablishmentId { get; set; }
    public int SmileyScore { get; set; }
    public DateOnly InspectedOn { get; set; }
    public DateTime RecordedAt { get; set; }

    public Establishment Establishment { get; set; } = null!;
}
