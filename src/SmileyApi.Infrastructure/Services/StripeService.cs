using Microsoft.Extensions.Configuration;
using SmileyApi.Core.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace SmileyApi.Infrastructure.Services;

public class StripeService : IStripeService
{
    private readonly string _priceId;

    public StripeService(IConfiguration config)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"]!;
        _priceId = config["Stripe:PriceId"]!;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        string businessId, string email, string successUrl, string cancelUrl)
    {
        var options = new SessionCreateOptions
        {
            CustomerEmail     = email,
            ClientReferenceId = businessId,
            Mode              = "subscription",
            LineItems         = [new SessionLineItemOptions { Price = _priceId, Quantity = 1 }],
            SuccessUrl        = successUrl,
            CancelUrl         = cancelUrl
        };
        var session = await new SessionService().CreateAsync(options);
        return session.Url;
    }

    public async Task<string> CreatePortalSessionAsync(string stripeCustomerId, string returnUrl)
    {
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer  = stripeCustomerId,
            ReturnUrl = returnUrl
        };
        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(options);
        return session.Url;
    }
}
