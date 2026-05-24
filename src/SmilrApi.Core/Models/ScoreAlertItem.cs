namespace SmilrApi.Core.Models;

public record ScoreAlertItem(
    string EstablishmentName,
    string? Address,
    string? CvrNumber,
    int OldScore,
    int NewScore);
