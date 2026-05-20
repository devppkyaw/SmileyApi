namespace SmileyApi.Core.Models;

public class Business
{
    public int Id { get; set; }
    public string BusinessId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Tier { get; set; } = "free";
    public bool IsEmailVerified { get; set; }
    public string? MagicLinkToken { get; set; }
    public DateTime? MagicLinkTokenExpiry { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime TermsAcceptedAt { get; set; }
    public bool MarketingConsentGiven { get; set; }
    public DateTime? MarketingConsentAt { get; set; }

    public ICollection<BusinessLocation> Locations { get; set; } = new List<BusinessLocation>();
}
