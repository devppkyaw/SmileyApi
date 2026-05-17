namespace SmileyApi.Core.Models;

public class WebhookSubscription
{
    public int Id { get; set; }
    public int ApiKeyId { get; set; }
    public int EstablishmentId { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ApiKey ApiKey { get; set; } = null!;
    public Establishment Establishment { get; set; } = null!;
}
