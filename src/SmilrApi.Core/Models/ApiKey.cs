namespace SmilrApi.Core.Models;

public class ApiKey
{
    public int Id { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string Tier { get; set; } = "free";
    public int RequestsToday { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTime LastResetAt { get; set; }
    public int? BusinessId { get; set; }
    public Business? Business { get; set; }
}
