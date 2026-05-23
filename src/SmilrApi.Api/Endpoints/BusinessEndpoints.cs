using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Infrastructure.Data;

namespace SmilrApi.Api.Endpoints;

public static class BusinessEndpoints
{
    private const string SessionKey = "business_id";

    public static void MapBusinessEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/business/register", async (
            RegisterRequest req,
            IBusinessService svc,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(Error("bad_request", "'email' is required."));
            if (string.IsNullOrWhiteSpace(req.CompanyName))
                return Results.BadRequest(Error("bad_request", "'companyName' is required."));
            if (!req.TermsAccepted)
                return Results.BadRequest(Error("terms_required", "You must accept the Terms of Service to register."));

            var baseUrl  = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var business = await svc.RegisterOrResendAsync(req.Email.Trim(), req.CompanyName, req.MarketingConsent, baseUrl, ct);

            if (business is null)
                return Results.Conflict(Error("already_registered", "This email is already registered. Use the login link instead."));

            return Results.Ok(new { message = "Verification email sent. Please check your inbox." });
        });

        app.MapGet("/v1/business/verify", async (
            string? token,
            IBusinessService svc,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.Redirect("/register.html?error=invalid_token");

            var business = await svc.VerifyEmailAsync(token, ct);
            if (business is null)
                return Results.Redirect("/register.html?error=invalid_token");

            ctx.Session.SetInt32(SessionKey, business.Id);
            return Results.Redirect("/dashboard.html");
        });

        app.MapPost("/v1/business/login", async (
            LoginRequest req,
            IBusinessService svc,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(Error("bad_request", "'email' is required."));

            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            // Always return the same message to prevent email enumeration
            await svc.RequestMagicLinkAsync(req.Email.Trim(), baseUrl, ct);
            return Results.Ok(new { message = "If that email is registered, a login link has been sent." });
        });

        app.MapGet("/v1/business/login/verify", async (
            string? token,
            IBusinessService svc,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.Redirect("/login.html?error=invalid_token");

            var business = await svc.VerifyMagicLinkAsync(token, ct);
            if (business is null)
                return Results.Redirect("/login.html?error=expired_token");

            ctx.Session.SetInt32(SessionKey, business.Id);
            return Results.Redirect("/dashboard.html");
        });

        app.MapPost("/v1/business/logout", (HttpContext ctx) =>
        {
            ctx.Session.Clear();
            return Results.Ok(new { message = "Logged out." });
        });

        app.MapGet("/v1/business/me", async (
            HttpContext ctx,
            IBusinessService svc,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            return Results.Ok(new
            {
                businessId  = business.BusinessId,
                email       = business.Email,
                companyName = business.CompanyName,
                tier        = business.Tier,
                createdAt   = business.CreatedAt
            });
        });

        app.MapPost("/v1/business/locations", async (
            AddLocationRequest req,
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            var exists = await db.Establishments.AnyAsync(e => e.Navnelbnr == req.Navnelbnr, ct);
            if (!exists)
                return Results.NotFound(Error("not_found", "No establishment found for this Navnelbnr."));

            var alreadyAdded = await db.BusinessLocations
                .AnyAsync(b => b.BusinessId == business.Id && b.Navnelbnr == req.Navnelbnr, ct);
            if (alreadyAdded)
                return Results.Conflict(Error("already_added", "This location is already in your account."));

            db.BusinessLocations.Add(new BusinessLocation
            {
                BusinessId = business.Id,
                Navnelbnr  = req.Navnelbnr,
                AddedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { navnelbnr = req.Navnelbnr });
        });

        app.MapPost("/v1/business/locations/by-cvr", async (
            AddByCvrRequest req,
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            if (business.Tier != "pro")
                return Results.Json(Error("forbidden", "CVR bulk onboard requires a Pro account."), statusCode: 403);

            if (string.IsNullOrWhiteSpace(req.Cvr))
                return Results.BadRequest(Error("bad_request", "'cvr' is required."));

            var navnelbnrs = await db.Establishments
                .Where(e => e.CvrNumber == req.Cvr.Trim())
                .Select(e => e.Navnelbnr)
                .ToListAsync(ct);

            if (navnelbnrs.Count == 0)
                return Results.NotFound(Error("not_found", "No establishments found for this CVR."));

            var existing = await db.BusinessLocations
                .Where(b => b.BusinessId == business.Id)
                .Select(b => b.Navnelbnr)
                .ToListAsync(ct);

            var toAdd = navnelbnrs.Except(existing).ToList();
            foreach (var navnelbnr in toAdd)
            {
                db.BusinessLocations.Add(new BusinessLocation
                {
                    BusinessId = business.Id,
                    Navnelbnr  = navnelbnr,
                    AddedAt    = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { added = toAdd.Count, total = navnelbnrs.Count });
        });

        app.MapDelete("/v1/business/locations/{navnelbnr:int}", async (
            int navnelbnr,
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            var entry = await db.BusinessLocations
                .FirstOrDefaultAsync(b => b.BusinessId == business.Id && b.Navnelbnr == navnelbnr, ct);

            if (entry is null)
                return Results.NotFound(Error("not_found", "Location not found in your account."));

            db.BusinessLocations.Remove(entry);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Location removed." });
        });

        app.MapGet("/v1/business/locations", async (
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            var locations = await db.BusinessLocations
                .Where(b => b.BusinessId == business.Id)
                .Join(db.Establishments,
                    bn => bn.Navnelbnr,
                    e  => e.Navnelbnr,
                    (bn, e) => new
                    {
                        navnelbnr   = e.Navnelbnr,
                        name        = e.Name,
                        address     = e.Address,
                        city        = e.City,
                        latestScore = e.LatestScore,
                        reportUrl   = e.ReportUrl,
                        addedAt     = bn.AddedAt
                    })
                .ToListAsync(ct);

            return Results.Ok(locations);
        });
    }

    private static async Task<Business?> GetSessionBusinessAsync(
        HttpContext ctx, IBusinessService svc, CancellationToken ct)
    {
        var id = ctx.Session.GetInt32(SessionKey);
        if (id is null) return null;
        return await svc.GetByIdAsync(id.Value, ct);
    }

    private static object Error(string code, string message) =>
        new { error = new { code, message } };
}

public record RegisterRequest(string Email, string CompanyName, bool TermsAccepted, bool MarketingConsent);
public record LoginRequest(string Email);
public record AddLocationRequest(int Navnelbnr);
public record AddByCvrRequest(string Cvr);
