namespace SmileyApi.Core.Interfaces;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(string businessId, string email, string successUrl, string cancelUrl);
    Task<string> CreatePortalSessionAsync(string stripeCustomerId, string returnUrl);
}
