using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Interfaces;
using SmilrApi.Infrastructure.Data;
using Stripe;
using Stripe.Checkout;

namespace SmilrApi.Api.Endpoints;

public static class StripeEndpoints
{
    private const string SessionKey = "business_id";

    public static void MapStripeEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/business/checkout", async (
            HttpContext ctx,
            IBusinessService businessSvc,
            IStripeService stripeSvc,
            CancellationToken ct) =>
        {
            var id = ctx.Session.GetInt32(SessionKey);
            if (id is null) return Results.Unauthorized();

            var business = await businessSvc.GetByIdAsync(id.Value, ct);
            if (business is null || !business.IsEmailVerified) return Results.Unauthorized();

            if (business.Tier == "pro")
                return Results.BadRequest(Error("already_pro", "Your account is already on the Pro tier."));

            var baseUrl    = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var successUrl = $"{baseUrl}/dashboard.html?upgraded=1";
            var cancelUrl  = $"{baseUrl}/dashboard.html";

            var url = await stripeSvc.CreateCheckoutSessionAsync(
                business.BusinessId, business.Email, successUrl, cancelUrl);

            return Results.Ok(new { url });
        });

        app.MapPost("/v1/business/portal", async (
            HttpContext ctx,
            IBusinessService businessSvc,
            IStripeService stripeSvc,
            CancellationToken ct) =>
        {
            var id = ctx.Session.GetInt32(SessionKey);
            if (id is null) return Results.Unauthorized();

            var business = await businessSvc.GetByIdAsync(id.Value, ct);
            if (business is null || !business.IsEmailVerified) return Results.Unauthorized();

            if (string.IsNullOrEmpty(business.StripeCustomerId))
                return Results.BadRequest(Error("no_subscription", "No active subscription found."));

            var baseUrl   = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var returnUrl = $"{baseUrl}/dashboard.html";

            var url = await stripeSvc.CreatePortalSessionAsync(business.StripeCustomerId, returnUrl);
            return Results.Ok(new { url });
        });

        app.MapPost("/v1/stripe/webhook", async (
            HttpContext ctx,
            SmilrDbContext db,
            IConfiguration config,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var json            = await new StreamReader(ctx.Request.Body).ReadToEndAsync(ct);
            var webhookSecret   = config["Stripe:WebhookSecret"]!;
            var stripeSignature = ctx.Request.Headers["Stripe-Signature"].ToString();

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
            }
            catch (StripeException ex)
            {
                logger.LogWarning("Stripe webhook signature validation failed: {Message}", ex.Message);
                return Results.BadRequest();
            }

            switch (stripeEvent.Type)
            {
                case EventTypes.CheckoutSessionCompleted:
                {
                    var session  = (Session)stripeEvent.Data.Object;
                    var business = await db.Businesses.FirstOrDefaultAsync(
                        b => b.BusinessId == session.ClientReferenceId, ct);

                    if (business is null)
                    {
                        logger.LogWarning("Stripe webhook: no business for client_reference_id={Id}", session.ClientReferenceId);
                        break;
                    }

                    business.Tier                 = "pro";
                    business.StripeCustomerId     = session.CustomerId;
                    business.StripeSubscriptionId = session.SubscriptionId;
                    var proKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.BusinessId == business.Id && k.IsActive, ct);
                    if (proKey is not null) proKey.Tier = "pro";
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Business {Id} upgraded to Pro via Stripe", business.BusinessId);
                    break;
                }

                case EventTypes.CustomerSubscriptionDeleted:
                {
                    var subscription = (Subscription)stripeEvent.Data.Object;
                    var business     = await db.Businesses.FirstOrDefaultAsync(
                        b => b.StripeCustomerId == subscription.CustomerId, ct);

                    if (business is null)
                    {
                        logger.LogWarning("Stripe webhook: no business for customer={Id}", subscription.CustomerId);
                        break;
                    }

                    business.Tier                 = "free";
                    business.StripeSubscriptionId = null;
                    var freeKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.BusinessId == business.Id && k.IsActive, ct);
                    if (freeKey is not null) freeKey.Tier = "free";
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Business {Id} reverted to Free (subscription cancelled)", business.BusinessId);
                    break;
                }

                default:
                    break;
            }

            return Results.Ok();
        });
    }

    private static object Error(string code, string message) =>
        new { error = new { code, message } };
}
