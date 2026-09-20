using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmilrApi.Api.Rendering;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;
using SmilrApi.Infrastructure.Data;

namespace SmilrApi.Api.Endpoints;

public static class BusinessEndpoints
{
    private const string SessionKey = "business_id";
    private const int RecentChangesWindowDays = RiskCalculator.RecentWindowDays;

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
            var business = await svc.RegisterOrResendAsync(req.Email.Trim(), req.CompanyName, req.MarketingConsent, baseUrl, req.ClaimCvr, ct);

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
            return Results.Redirect(DashboardRedirectPath(business.PendingClaimCvr));
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
            var sent = await svc.RequestMagicLinkAsync(req.Email.Trim(), baseUrl, req.ClaimCvr, ct);
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
            return Results.Redirect(DashboardRedirectPath(business.PendingClaimCvr));
        });

        app.MapPost("/v1/business/logout", (HttpContext ctx) =>
        {
            ctx.Session.Clear();
            return Results.Ok(new { message = "Logged out." });
        });

        app.MapGet("/v1/business/me", async (
            HttpContext ctx,
            IBusinessService svc,
            IApiKeyService apiKeySvc,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            // Developer API is Pro/Enterprise only — except a Free account that already has a key
            // (grandfathered from before it was gated) still gets to view/rotate/revoke it. Resolved
            // here so the shared nav can show/hide the link without its own /apikey round trip.
            var canUseDeveloperApi = business.Tier != "free"
                || await apiKeySvc.GetForBusinessAsync(business.Id, ct) is not null;

            return Results.Ok(new
            {
                businessId  = business.BusinessId,
                email       = business.Email,
                companyName = business.CompanyName,
                tier        = business.Tier,
                createdAt   = business.CreatedAt,
                canUseDeveloperApi
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

            // Developer API access requires Pro/Enterprise — except a Free business that already
            // has a key (grandfathered from before this was gated) may still rotate it, just not
            // acquire a first one.
            if (business.Tier == "free")
            {
                var existingKey = await apiKeySvc.GetForBusinessAsync(business.Id, ct);
                if (existingKey is null)
                    return Results.Json(
                        Error("pro_required", "Developer API access requires a Pro or Enterprise plan."),
                        statusCode: 403);
            }

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

        // Paged: only the requested page's establishments get their inspection rows loaded (<= 100),
        // instead of every inspection row for every location the business owns. Default sort is by CVR
        // (then name) so a CVR group stays contiguous across pages; cvrGroups carries each group's
        // full filtered size so the group header count is right even when the group spans pages.
        //
        // Every row carries its RiskCalculator assessment (badge), `attention=1` restricts the list to the
        // Overview's "Needs attention" set, and sort=risk orders most-concerning first. Risk is assessed
        // for the whole portfolio (one owned-locations read + one transitions query), because the filter
        // and the sort span pages.
        app.MapGet("/v1/business/locations", async (
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            IEstablishmentRepository establishments,
            int? page,
            int? pageSize,
            string? q,
            string? sort,
            int? attention,
            string? cvr,
            int? groupsOnly,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            var (pageNo, size) = LocationListPaging.Normalize(page, pageSize);
            var sortMode = LocationListPaging.NormalizeSort(sort);
            var search   = LocationListPaging.NormalizeSearch(q);
            var attentionOnly = attention == 1;
            var cvrFilter = LocationListPaging.NormalizeCvr(cvr);

            var risk = await AssessRiskAsync(db, establishments, business.Id, ct);
            var attentionIds = risk.Where(kv => kv.Value.NeedsAttention).Select(kv => kv.Key).ToList();

            var all = db.BusinessLocations
                .Where(b => b.BusinessId == business.Id)
                .Join(db.Establishments,
                    bn => bn.Navnelbnr,
                    e  => e.Navnelbnr,
                    (bn, e) => new { e, bn.AddedAt });

            var locationCount = await all.CountAsync(ct);
            var cvrCount = await all
                .Where(x => x.e.CvrNumber != null)
                .Select(x => x.e.CvrNumber)
                .Distinct()
                .CountAsync(ct);

            var filtered = all;
            if (search is not null)
            {
                filtered = filtered.Where(x =>
                    x.e.Name.Contains(search) ||
                    (x.e.Address != null && x.e.Address.Contains(search)) ||
                    (x.e.City != null && x.e.City.Contains(search)) ||
                    (x.e.CvrNumber != null && x.e.CvrNumber.Contains(search)) ||
                    x.e.Navnelbnr.ToString().Contains(search));
            }

            // "Needs attention (N)" counts the needs-attention locations matching the current search, so the
            // number always equals what selecting that filter shows (the portfolio total when not searching).
            var attentionCount = search is null
                ? attentionIds.Count
                : await filtered.Where(x => attentionIds.Contains(x.e.Navnelbnr)).CountAsync(ct);

            if (attentionOnly)
                filtered = filtered.Where(x => attentionIds.Contains(x.e.Navnelbnr));

            // The CVR groups (with counts under the q / attention filters) are what the grouped view lists.
            // Computed before the single-CVR restriction below, and skipped when one group's rows are asked for.
            var cvrGroups = new List<CvrGroupCount>();
            if (cvrFilter is null)
            {
                cvrGroups = (await filtered
                        .GroupBy(x => x.e.CvrNumber)
                        .Select(g => new { cvr = g.Key, count = g.Count() })
                        .ToListAsync(ct))
                    .OrderBy(g => g.cvr == null).ThenBy(g => g.cvr)
                    .Select(g => new CvrGroupCount(g.cvr, g.count))
                    .ToList();
            }

            // One group's rows: restrict to that CVR ("none" = the locations without one).
            if (cvrFilter == LocationListPaging.NoCvr)
                filtered = filtered.Where(x => x.e.CvrNumber == null);
            else if (cvrFilter is not null)
                filtered = filtered.Where(x => x.e.CvrNumber == cvrFilter);

            var total = search is null && !attentionOnly && cvrFilter is null
                ? locationCount
                : await filtered.CountAsync(ct);

            // Grouped view's first call: headers and counts only — no rows, no inspection history.
            if (groupsOnly == 1)
            {
                return Results.Ok(new
                {
                    locations = new List<object>(),
                    groupsOnly = true,   // echoed so the page can tell a server that predates this mode
                    cvrFilter,
                    locationCount,
                    cvrCount,
                    total,
                    attentionCount,
                    page = pageNo,
                    pageSize = size,
                    cvrGroups
                });
            }

            var ordered = sortMode switch
            {
                LocationListPaging.SortScoreAsc => filtered
                    .OrderBy(x => x.e.LatestScore == null).ThenBy(x => x.e.LatestScore)
                    .ThenBy(x => x.e.Name).ThenBy(x => x.e.Navnelbnr),
                LocationListPaging.SortScoreDesc => filtered
                    .OrderBy(x => x.e.LatestScore == null).ThenByDescending(x => x.e.LatestScore)
                    .ThenBy(x => x.e.Name).ThenBy(x => x.e.Navnelbnr),
                _ => filtered
                    .OrderBy(x => x.e.CvrNumber == null).ThenBy(x => x.e.CvrNumber)
                    .ThenBy(x => x.e.Name).ThenBy(x => x.e.Navnelbnr),
            };

            List<LocationRow> pageRows;
            if (sortMode == LocationListPaging.SortRisk)
            {
                // Risk rank lives in memory, not SQL: order the (id, name) pairs, page them, then load
                // just that page's rows and put them back in rank order.
                var candidates = await filtered.Select(x => new { x.e.Navnelbnr, x.e.Name }).ToListAsync(ct);
                var pageIds = candidates
                    .OrderBy(c => risk.GetValueOrDefault(c.Navnelbnr, LocationRisk.None).Rank)
                    .ThenBy(c => c.Name).ThenBy(c => c.Navnelbnr)
                    .Skip((pageNo - 1) * size).Take(size)
                    .Select(c => c.Navnelbnr).ToList();

                var loaded = await all
                    .Where(x => pageIds.Contains(x.e.Navnelbnr))
                    .Select(x => new LocationRow(
                        x.e.Id, x.e.Navnelbnr, x.e.CvrNumber, x.e.Name, x.e.Address, x.e.City,
                        x.e.LatestScore, x.e.LatestScoreDate, x.e.ReportUrl, x.e.VirksomhedsType, x.AddedAt))
                    .ToListAsync(ct);
                var byNavnelbnr = loaded.ToDictionary(r => r.Navnelbnr);
                pageRows = pageIds.Select(id => byNavnelbnr[id]).ToList();
            }
            else
            {
                pageRows = await ordered
                    .Skip((pageNo - 1) * size)
                    .Take(size)
                    .Select(x => new LocationRow(
                        x.e.Id, x.e.Navnelbnr, x.e.CvrNumber, x.e.Name, x.e.Address, x.e.City,
                        x.e.LatestScore, x.e.LatestScoreDate, x.e.ReportUrl, x.e.VirksomhedsType, x.AddedAt))
                    .ToListAsync(ct);
            }

            var estIds = pageRows.Select(l => l.EstId).ToList();

            var inspections = estIds.Count == 0
                ? []
                : await db.Inspections
                    .Where(i => estIds.Contains(i.EstablishmentId))
                    .Select(i => new { i.EstablishmentId, i.InspectedOn, i.SmileyScore })
                    .ToListAsync(ct);

            var inspectionsByEst = inspections
                .GroupBy(i => i.EstablishmentId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.InspectedOn).ToList());

            var locationItems = pageRows.Select(l =>
            {
                var history = inspectionsByEst.GetValueOrDefault(l.EstId) ?? [];
                return new
                {
                    navnelbnr       = l.Navnelbnr,
                    cvrNumber       = l.CvrNumber,
                    name            = l.Name,
                    address         = l.Address,
                    city            = l.City,
                    latestScore     = l.LatestScore,
                    latestScoreDate = l.LatestScoreDate,
                    reportUrl       = l.ReportUrl,
                    virksomhedsType = l.VirksomhedsType,
                    addedAt         = l.AddedAt,
                    risk            = RiskDto(risk.GetValueOrDefault(l.Navnelbnr, LocationRisk.None)),
                    scoreHistory    = history.Take(4).Select(i => new { date = i.InspectedOn, score = i.SmileyScore }).ToList(),
                    inspectionCount = history.Count
                };
            }).ToList();

            return Results.Ok(new
            {
                locations = locationItems,
                cvrFilter,   // echoed so the page can tell a server that ignores the cvr parameter
                locationCount,
                cvrCount,
                total,
                attentionCount,
                page = pageNo,
                pageSize = size,
                cvrGroups
            });
        });

        // Landing-page aggregate for the dashboard: everything is computed in SQL and no per-location
        // rows are returned beyond the handful of "needs attention" entries, so this stays light no
        // matter how many locations the business has. Smiley scores run 1 (best) to 4 (worst).
        app.MapGet("/v1/business/overview", async (
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            IEstablishmentRepository establishments,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();

            var owned = db.BusinessLocations
                .Where(b => b.BusinessId == business.Id)
                .Join(db.Establishments, bn => bn.Navnelbnr, e => e.Navnelbnr, (bn, e) => e);

            var distributionRows = await owned
                .GroupBy(e => e.LatestScore)
                .Select(g => new { score = g.Key, count = g.Count() })
                .ToListAsync(ct);

            var locationCount = distributionRows.Sum(r => r.count);
            var cvrCount = await owned
                .Where(e => e.CvrNumber != null)
                .Select(e => e.CvrNumber)
                .Distinct()
                .CountAsync(ct);

            var scored = distributionRows.Where(r => r.score != null).ToList();
            var scoredCount = scored.Sum(r => r.count);
            double? averageScore = scoredCount == 0
                ? null
                : Math.Round(scored.Sum(r => (double)r.score!.Value * r.count) / scoredCount, 1);

            var scoreDistribution = new
            {
                s1 = distributionRows.Where(r => r.score == 1).Sum(r => r.count),
                s2 = distributionRows.Where(r => r.score == 2).Sum(r => r.count),
                s3 = distributionRows.Where(r => r.score == 3).Sum(r => r.count),
                s4 = distributionRows.Where(r => r.score == 4).Sum(r => r.count),
                unscored = distributionRows.Where(r => r.score == null).Sum(r => r.count)
            };

            // The "Score 3 or 4" tile counts current scores only; the "Needs attention" panel is the wider
            // RiskCalculator set (score 3/4, declined in the last 90 days, no score yet).
            var highScoreCount = distributionRows.Where(r => r.score >= 3).Sum(r => r.count);

            var risk = await AssessRiskAsync(db, establishments, business.Id, ct);
            var attentionIds = risk.Where(kv => kv.Value.NeedsAttention).Select(kv => kv.Key).ToList();

            // Ties inside a rank (e.g. several locations at score 4) are broken by name, so names are needed
            // for the whole attention set — id + name only — before the top 5 get their full rows.
            var attentionNames = attentionIds.Count == 0
                ? []
                : await owned.Where(e => attentionIds.Contains(e.Navnelbnr))
                    .Select(e => new { e.Navnelbnr, e.Name }).ToListAsync(ct);
            var topIds = attentionNames
                .OrderBy(n => risk[n.Navnelbnr].Rank).ThenBy(n => n.Name).ThenBy(n => n.Navnelbnr)
                .Take(5).Select(n => n.Navnelbnr).ToList();
            var topRows = topIds.Count == 0
                ? []
                : await owned.Where(e => topIds.Contains(e.Navnelbnr))
                    .Select(e => new { e.Navnelbnr, e.Name, e.City, e.LatestScore, e.LatestScoreDate, e.VirksomhedsType })
                    .ToListAsync(ct);
            var topById = topRows.ToDictionary(r => r.Navnelbnr);
            var needsAttention = topIds.Select(id =>
            {
                var r = topById[id];
                return new
                {
                    navnelbnr = r.Navnelbnr,
                    name = r.Name,
                    city = r.City,
                    detailPath = FindUrlBuilder.DetailPath(r.Name, r.City, r.Navnelbnr),
                    virksomhedsType = r.VirksomhedsType,
                    latestScore = r.LatestScore,
                    latestScoreDate = r.LatestScoreDate,
                    risk = RiskDto(risk[id])
                };
            }).ToList();

            var windowStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-RecentChangesWindowDays);
            var changeRows = locationCount == 0
                ? []
                : await establishments.GetRecentChangesForBusinessAsync(business.Id, windowStart, 10, ct);

            var recentChanges = changeRows.Select(c => new
            {
                navnelbnr = c.Establishment.Navnelbnr,
                name = c.Establishment.Name,
                city = c.Establishment.City,
                virksomhedsType = c.Establishment.VirksomhedsType,
                detailPath = FindUrlBuilder.DetailPath(c.Establishment),
                previousScore = c.PreviousScore,
                newScore = c.NewScore,
                changeDate = c.ChangeDate,
                improved = c.NewScore < c.PreviousScore
            }).ToList();

            return Results.Ok(new
            {
                locationCount,
                cvrCount,
                averageScore,
                scoreDistribution,
                highScoreCount,
                needsAttentionCount = attentionIds.Count,
                needsAttention,
                recentChangesWindowDays = RecentChangesWindowDays,
                recentChanges
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

        // ── Benchmarking (Pro/Enterprise) ─────────────────────────────────────────────────────────
        // Loaded lazily by the Overview / Locations pages, separate from /overview, so the landing page
        // stays one light call. Peers: same Pixibranche category in the same City, else the same
        // category nationwide (see BenchmarkCalculator).
        app.MapGet("/v1/business/benchmark", async (
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            IEstablishmentRepository establishments,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();
            if (business.Tier == "free") return ProRequired();

            var locations = await LoadBenchmarkInputsAsync(db, business.Id, null, ct);
            var benchmarks = await BenchmarkLocationsAsync(locations, establishments, cache, ct);

            var portfolio = BenchmarkCalculator.Rollup(benchmarks.Values.ToList(), locations.Count);

            var belowPeers = locations
                .Where(l => benchmarks.TryGetValue(l.Navnelbnr, out var b) && b.Score - b.PeerAverage > BenchmarkCalculator.InLineTolerance)
                .Select(l => (Loc: l, Bench: benchmarks[l.Navnelbnr]))
                .OrderByDescending(x => x.Bench.Score - x.Bench.PeerAverage).ThenBy(x => x.Loc.Name)
                .Take(5)
                .Select(x => new
                {
                    navnelbnr = x.Loc.Navnelbnr,
                    name = x.Loc.Name,
                    city = x.Loc.City,
                    detailPath = FindUrlBuilder.DetailPath(x.Loc.Name, x.Loc.City, x.Loc.Navnelbnr),
                    score = x.Bench.Score,
                    peerAverage = Math.Round(x.Bench.PeerAverage, 2),
                    scope = ScopeName(x.Bench.Scope),
                    peerCount = x.Bench.PeerCount
                })
                .ToList();

            return Results.Ok(new
            {
                totalLocations = locations.Count,
                benchmarkedLocations = benchmarks.Count,
                portfolio,
                belowPeers
            });
        });

        app.MapGet("/v1/business/locations/{navnelbnr:int}/benchmark", async (
            int navnelbnr,
            HttpContext ctx,
            IBusinessService svc,
            SmilrDbContext db,
            IEstablishmentRepository establishments,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var business = await GetSessionBusinessAsync(ctx, svc, ct);
            if (business is null) return Results.Unauthorized();
            if (business.Tier == "free") return ProRequired();

            var location = (await LoadBenchmarkInputsAsync(db, business.Id, navnelbnr, ct)).FirstOrDefault();
            if (location is null) return Results.NotFound();

            var benchmarks = await BenchmarkLocationsAsync([location], establishments, cache, ct);
            if (!benchmarks.TryGetValue(navnelbnr, out var b))
            {
                return Results.Ok(new
                {
                    benchmarked = false,
                    reason = "There aren't enough comparable establishments (scored, in the same category) to benchmark this location."
                });
            }

            return Results.Ok(new
            {
                benchmarked = true,
                navnelbnr,
                name = location.Name,
                city = location.City,
                category = location.Pixibranche,
                score = b.Score,
                scope = ScopeName(b.Scope),
                peerCount = b.PeerCount,
                peers = new { s1 = b.Peers.S1, s2 = b.Peers.S2, s3 = b.Peers.S3, s4 = b.Peers.S4 },
                peerAverage = Math.Round(b.PeerAverage, 2),
                peerTopSharePercent = b.PeerTopSharePercent,
                samePercent = b.SamePercent,
                peersBetterPercent = b.PeersBetterPercent,
                peersWorsePercent = b.PeersWorsePercent
            });
        });
    }

    private sealed record CvrGroupCount(string? Cvr, int Count);

    private sealed record LocationRow(
        int EstId, int Navnelbnr, string? CvrNumber, string Name, string? Address, string? City,
        int? LatestScore, DateOnly? LatestScoreDate, string? ReportUrl, string? VirksomhedsType, DateTime AddedAt);

    // One RiskCalculator assessment per location in the business (keyed by Navnelbnr): one owned-locations
    // read plus one recent-transitions query. Shared by the Overview's "Needs attention" panel and the
    // Locations list (badges, needs-attention filter, risk sort) so both always agree.
    private static async Task<Dictionary<int, LocationRisk>> AssessRiskAsync(
        SmilrDbContext db, IEstablishmentRepository establishments, int businessId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var locations = await db.BusinessLocations
            .Where(b => b.BusinessId == businessId)
            .Join(db.Establishments, bn => bn.Navnelbnr, e => e.Navnelbnr,
                (bn, e) => new { e.Id, e.Navnelbnr, e.LatestScore, IsDelisted = e.DelistedAt != null })
            .ToListAsync(ct);
        if (locations.Count == 0) return [];

        var transitions = await establishments.GetRecentTransitionsForBusinessAsync(
            businessId, today.AddDays(-RiskCalculator.RecentWindowDays), ct);
        var byEstablishment = transitions
            .GroupBy(t => t.EstablishmentId)
            .ToDictionary(g => g.Key, g => g.Select(t => new ScoreTransition(t.ChangeDate, t.PreviousScore, t.NewScore)).ToList());

        var result = new Dictionary<int, LocationRisk>(locations.Count);
        foreach (var l in locations)
        {
            result[l.Navnelbnr] = RiskCalculator.Assess(
                l.LatestScore, byEstablishment.GetValueOrDefault(l.Id) ?? [], today, l.IsDelisted);
        }
        return result;
    }

    // Wire shape for a location's risk: level plus reason codes (the UI words them; declines carry the
    // date of the newest in-window downgrade).
    private static object RiskDto(LocationRisk r) => new
    {
        level = r.Level switch { RiskLevel.AtRisk => "at_risk", RiskLevel.Watch => "watch", _ => "ok" },
        reasons = r.Reasons.Select(reason => new
        {
            code = reason switch
            {
                RiskReason.HighScore => "high_score",
                RiskReason.ConsecutiveDeclines => "consecutive_declines",
                RiskReason.RecentDecline => "recent_decline",
                _ => "no_score",
            },
            date = reason is RiskReason.ConsecutiveDeclines or RiskReason.RecentDecline ? r.DeclineDate : null
        }).ToList()
    };

    private sealed record BenchmarkInput(
        int Navnelbnr, string Name, string? City, string? Pixibranche, int? LatestScore, bool InPeerPopulation);

    private static IResult ProRequired() =>
        Results.Json(Error("pro_required", "Benchmarking is available on the Pro plan."), statusCode: StatusCodes.Status403Forbidden);

    private static string ScopeName(BenchmarkScope scope) => scope == BenchmarkScope.Area ? "area" : "national";

    // One business's locations (optionally just one) as the small projection benchmarking needs.
    // InPeerPopulation mirrors EstablishmentRepository.PeerPopulation's CvrNumber/DelistedAt rules, i.e.
    // whether the location is itself counted in its peer group and must be subtracted from it.
    private static async Task<List<BenchmarkInput>> LoadBenchmarkInputsAsync(
        SmilrDbContext db, int businessId, int? navnelbnr, CancellationToken ct)
    {
        var owned = db.BusinessLocations
            .Where(b => b.BusinessId == businessId && (navnelbnr == null || b.Navnelbnr == navnelbnr))
            .Join(db.Establishments, bn => bn.Navnelbnr, e => e.Navnelbnr, (bn, e) => e);

        return await owned
            .Select(e => new BenchmarkInput(
                e.Navnelbnr, e.Name, e.City, e.Pixibranche, e.LatestScore,
                e.CvrNumber != null && e.DelistedAt == null))
            .ToListAsync(ct);
    }

    // Benchmarks every eligible location (scored 1-4, has a City and a real category) and returns them by
    // Navnelbnr; locations without enough peers are simply absent. Two grouped queries at most: the
    // (City, category) peer groups for the locations, plus the nationwide per-category distribution
    // (cached — it's ~26 rows and only moves when the feed syncs).
    private static async Task<Dictionary<int, LocationBenchmark>> BenchmarkLocationsAsync(
        IReadOnlyCollection<BenchmarkInput> locations, IEstablishmentRepository establishments,
        IMemoryCache cache, CancellationToken ct)
    {
        var eligible = locations
            .Where(l => l.LatestScore is >= 1 and <= 4
                     && !string.IsNullOrWhiteSpace(l.City)
                     && !string.IsNullOrWhiteSpace(l.Pixibranche)
                     && !PixibrancheCategories.IsPlaceholder(l.Pixibranche))
            .ToList();
        if (eligible.Count == 0) return [];

        var area = await establishments.GetPeerScoreDistributionsAsync(
            eligible.Select(l => l.City!).Distinct().ToList(),
            eligible.Select(l => l.Pixibranche!).Distinct().ToList(), ct);

        var national = await cache.GetOrCreateAsync("benchmark:national-category-distributions", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            return establishments.GetNationalCategoryDistributionsAsync(ct);
        }) ?? new Dictionary<string, ScoreDistribution>();

        var result = new Dictionary<int, LocationBenchmark>();
        foreach (var l in eligible)
        {
            ScoreDistribution? areaDist = area.TryGetValue(BenchmarkCalculator.PeerKey(l.City!, l.Pixibranche!), out var a) ? a : null;
            ScoreDistribution? nationalDist = national.TryGetValue(l.Pixibranche!.Trim().ToLowerInvariant(), out var n) ? n : null;

            var b = BenchmarkCalculator.ForLocation(l.LatestScore!.Value, areaDist, nationalDist, l.InPeerPopulation);
            if (b is not null) result[l.Navnelbnr] = b;
        }
        return result;
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

    // overview.html (the dashboard landing page) already knows how to consume ?claim_cvr= (added for
    // the already-logged-in claim flow) — reused here so a pending claim captured at
    // registration/login time gets attached the moment a session actually exists, instead of being
    // silently dropped.
    private static string DashboardRedirectPath(string? pendingClaimCvr) =>
        string.IsNullOrWhiteSpace(pendingClaimCvr)
            ? "/overview.html"
            : "/overview.html?claim_cvr=" + Uri.EscapeDataString(pendingClaimCvr);
}

public record RegisterRequest(string Email, string CompanyName, bool TermsAccepted, bool MarketingConsent, string? ClaimCvr = null);
public record LoginRequest(string Email, string? ClaimCvr = null);
public record AddLocationRequest(int Navnelbnr);
public record AddByCvrRequest(string Cvr);
public record RemoveByCvrRequest(string? Cvr);
