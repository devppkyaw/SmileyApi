using Microsoft.AspNetCore.Mvc;
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
            var sent = await svc.RequestMagicLinkAsync(req.Email.Trim(), baseUrl, ct);
            if (!sent)
                return Results.NotFound(Error("not_registered", "No account found for that email."));
            return Results.Ok(new { message = "A login link has been sent." });
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

        app.MapPost("/v1/business/apikey/generate", async (
            HttpContext ctx,
            IBusinessService svc,
            IApiKeyService apiKeySvc,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null || !business.IsEmailVerified) return Results.Unauthorized();

            var (plaintext, _) = await apiKeySvc.GenerateForBusinessAsync(business, ct);
            return Results.Ok(new { key = plaintext, tier = business.Tier });
        });

        app.MapGet("/v1/business/apikey", async (
            HttpContext ctx,
            IBusinessService svc,
            IApiKeyService apiKeySvc,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            var apiKey = await apiKeySvc.GetForBusinessAsync(business.Id, ct);
            if (apiKey is null)
                return Results.Ok(new { hasKey = false });

            return Results.Ok(new { hasKey = true, tier = apiKey.Tier, createdAt = apiKey.CreatedAt });
        });

        app.MapDelete("/v1/business/apikey", async (
            HttpContext ctx,
            IBusinessService svc,
            IApiKeyService apiKeySvc,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            var revokedKey = await apiKeySvc.RevokeForBusinessAsync(business.Id, ct);
            if (revokedKey is null) return Results.NotFound(Error("not_found", "No active API key found."));
            return Results.Ok(new { message = "API key revoked.", revokedAt = revokedKey.RevokedAt });
        });

        app.MapPost("/v1/business/locations", async (
            AddLocationRequest req,
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            if (business.Tier != "pro")
                return Results.Json(Error("forbidden", "Adding locations by Navnelbnr requires a Pro account."), statusCode: 403);

            var proCvrCap = config.GetValue<int>("Tiers:ProCvrCap", 20);
            var estCvr = await db.Establishments
                .Where(e => e.Navnelbnr == req.Navnelbnr)
                .Select(e => e.CvrNumber)
                .FirstOrDefaultAsync(ct);

            if (estCvr != null)
            {
                var cvrAlreadyInPortfolio = await db.BusinessLocations
                    .Where(bl => bl.BusinessId == business.Id)
                    .Join(db.Establishments, bl => bl.Navnelbnr, e => e.Navnelbnr, (bl, e) => e.CvrNumber)
                    .AnyAsync(cvr => cvr == estCvr, ct);

                if (!cvrAlreadyInPortfolio)
                {
                    var cvrCount = await db.BusinessLocations
                        .Where(bl => bl.BusinessId == business.Id)
                        .Join(db.Establishments, bl => bl.Navnelbnr, e => e.Navnelbnr, (bl, e) => e.CvrNumber)
                        .Where(cvr => cvr != null)
                        .Distinct()
                        .CountAsync(ct);

                    if (cvrCount >= proCvrCap)
                        return Results.Json(
                            Error("cvr_limit_reached", $"Pro accounts support up to {proCvrCap} CVRs. Contact us at info@smilrhq.dk for Enterprise."),
                            statusCode: 402);
                }
            }

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
            IConfiguration config,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Cvr))
                return Results.BadRequest(Error("bad_request", "'cvr' is required."));

            if (business.Tier != "pro")
            {
                var existingCvrs = await db.BusinessLocations
                    .Where(b => b.BusinessId == business.Id)
                    .Join(db.Establishments,
                        bl => bl.Navnelbnr,
                        e  => e.Navnelbnr,
                        (bl, e) => e.CvrNumber)
                    .Where(cvr => cvr != null)
                    .Distinct()
                    .ToListAsync(ct);

                if (existingCvrs.Count >= 1 && !existingCvrs.Contains(req.Cvr.Trim()))
                    return Results.Json(
                        Error("cvr_limit_reached", "Free accounts are limited to one CVR."),
                        statusCode: 403);
            }
            else
            {
                var proCvrCap    = config.GetValue<int>("Tiers:ProCvrCap", 20);
                var existingCvrs = await db.BusinessLocations
                    .Where(bl => bl.BusinessId == business.Id)
                    .Join(db.Establishments, bl => bl.Navnelbnr, e => e.Navnelbnr, (bl, e) => e.CvrNumber)
                    .Where(cvr => cvr != null)
                    .Distinct()
                    .ToListAsync(ct);

                if (!existingCvrs.Contains(req.Cvr.Trim()) && existingCvrs.Count >= proCvrCap)
                    return Results.Json(
                        Error("cvr_limit_reached", $"Pro accounts support up to {proCvrCap} CVRs. Contact us at info@smilrhq.dk for Enterprise."),
                        statusCode: 402);
            }

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

        app.MapDelete("/v1/business/locations/by-cvr", async (
            [FromBody] RemoveByCvrRequest req,
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Cvr))
                return Results.BadRequest(Error("bad_request", "'cvr' is required."));

            var navnelbnrs = await db.Establishments
                .Where(e => e.CvrNumber == req.Cvr.Trim())
                .Select(e => e.Navnelbnr)
                .ToListAsync(ct);

            var toRemove = await db.BusinessLocations
                .Where(b => b.BusinessId == business.Id && navnelbnrs.Contains(b.Navnelbnr))
                .ToListAsync(ct);

            db.BusinessLocations.RemoveRange(toRemove);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { removed = toRemove.Count });
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
                        estId           = e.Id,
                        navnelbnr       = e.Navnelbnr,
                        cvrNumber       = e.CvrNumber,
                        name            = e.Name,
                        address         = e.Address,
                        city            = e.City,
                        latestScore     = e.LatestScore,
                        latestScoreDate = e.LatestScoreDate,
                        reportUrl       = e.ReportUrl,
                        virksomhedsType = e.VirksomhedsType,
                        addedAt         = bn.AddedAt
                    })
                .ToListAsync(ct);

            var estIds = locations.Select(l => l.estId).ToList();

            var inspections = await db.Inspections
                .Where(i => estIds.Contains(i.EstablishmentId))
                .Select(i => new { i.EstablishmentId, i.InspectedOn, i.SmileyScore })
                .ToListAsync(ct);

            var inspectionsByEst = inspections
                .GroupBy(i => i.EstablishmentId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.InspectedOn).ToList());

            var locationItems = locations.Select(l =>
            {
                var all = inspectionsByEst.GetValueOrDefault(l.estId) ?? [];
                return new
                {
                    l.navnelbnr,
                    l.cvrNumber,
                    l.name,
                    l.address,
                    l.city,
                    l.latestScore,
                    l.latestScoreDate,
                    l.reportUrl,
                    l.virksomhedsType,
                    l.addedAt,
                    scoreHistory = all.Take(4).Select(i => new { date = i.InspectedOn, score = i.SmileyScore }).ToList(),
                    inspectionCount = all.Count
                };
            }).ToList();

            var cvrCount = locationItems.Select(l => l.cvrNumber).Where(c => c != null).Distinct().Count();
            return Results.Ok(new
            {
                locations     = locationItems,
                locationCount = locationItems.Count,
                cvrCount
            });
        });

        app.MapGet("/v1/business/locations/{navnelbnr:int}/history", async (
            int navnelbnr,
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            var owns = await db.BusinessLocations
                .AnyAsync(bl => bl.BusinessId == business.Id && bl.Navnelbnr == navnelbnr, ct);
            if (!owns) return Results.NotFound();

            var est = await db.Establishments
                .Where(e => e.Navnelbnr == navnelbnr)
                .Select(e => new { e.Id, e.Name })
                .FirstOrDefaultAsync(ct);
            if (est is null) return Results.NotFound();

            var history = await db.Inspections
                .Where(i => i.EstablishmentId == est.Id)
                .OrderByDescending(i => i.InspectedOn)
                .Select(i => new { date = i.InspectedOn, score = i.SmileyScore })
                .ToListAsync(ct);

            return Results.Ok(new { navnelbnr, name = est.Name, history });
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
public record RemoveByCvrRequest(string? Cvr);
