# CLAUDE.md

Instructions for Claude when working on this codebase.

## Stack
- ASP.NET Core Minimal API (C#)
- Entity Framework Core with SQL Server
- BackgroundService for XML sync worker

## Project Structure
- `src/SmilrApi.Api/` — API endpoints, middleware, worker, static landing page
- `src/SmilrApi.Core/` — domain models and interfaces (no EF dependency)
- `src/SmilrApi.Infrastructure/` — EF Core DbContext, migrations, repositories

## Common Commands
```bash
# Run the API locally
dotnet run --project src/SmilrApi.Api

# Add a new EF migration
dotnet ef migrations add <Name> --project src/SmilrApi.Infrastructure --startup-project src/SmilrApi.Api

# Apply migrations
dotnet ef database update --project src/SmilrApi.Infrastructure --startup-project src/SmilrApi.Api

# Run tests
dotnet test
```

## Key Rules
- **API keys:** always store as SHA-256 hash — never plaintext, never logged
- **Worker:** `XmlSyncWorker` is a `BackgroundService` inside `SmilrApi.Api/Workers/`. Do not move it to a separate project yet.
- **XML source URL:** `https://www.foedevarestyrelsen.dk/Media/638212360788086849/Smiley_xml.xml`
- **DbContext in worker:** always resolve via `IServiceScopeFactory` — never inject `SmilrDbContext` directly into the worker
- **Bulk inserts:** never insert XML sync rows one-by-one with EF. Use raw `MERGE` SQL via `SqlBulkCopy` + temp tables (implemented in `EstablishmentSyncService`). Do NOT use `EFCore.BulkExtensions` — v10 is a .NET 10-only meta-package; no compatible version is referenced.
- **Natural key:** use `navnelbnr` (Fødevarestyrelsen's ID) for upsert diffing — not CVR, which can be missing
- **Geo (MVP):** lat/lng stored as `float` columns, Haversine in raw SQL. Do not add `geography` column yet.
- **Error responses:** always return `{ "error": { "code": "...", "message": "..." } }` — no other shape
- **No frontend framework:** landing page is plain HTML/CSS in `wwwroot/` — do not introduce React or similar
- **Cookie consent:** whenever a change adds/removes a cookie, client-side storage, tracking pixel, or third-party script (analytics, embeds, ads, etc.), check whether `src/SmilrApi.Api/wwwroot/privacy.html` (Cookies + Legal basis sections) still accurately describes it, and update it in the same change. If the new tech is NOT cookie-free/strictly-necessary/otherwise consent-exempt, it needs a real opt-in consent banner before shipping — don't just add a disclosure sentence and call it done.

## What Is Post-MVP (Do Not Build Yet)
- Postal-code neighbourhood scores
- Reliability trend scoring
- CVR enrichment
- Splitting the worker into a separate project

## Roadmap Note (2026-08-18)
Priority after MVP: a public, no-login directory site ("Smilr Finder") as a top-of-funnel for Business subscriptions — reuses existing `/search`, `/nearby`, `/{cvr}/history` logic behind a new anonymous/cached route group, with a "Claim this listing" CTA into the registration flow. Depends on Phase H static pages shipping first. Full plan in `docs/business-opportunities.md`. API marketplace listing (RapidAPI-style) is a separate, lower-priority idea — do not build unless explicitly asked.

## Multi-Session Editing (2026-08-19)
This project is worked on from two places at once, often around the same time: Claude Code (this CLI, local) and a separate Claude session in the Cowork/desktop app. Both can edit files in this repo independently — most often `docs/business-opportunities.md`, which is the shared planning/roadmap doc for both.

**Rule: never blind-overwrite a shared doc.** Before writing to `docs/business-opportunities.md` (or any file either side edits regularly), re-read its current on-disk content first — it may have changed since you last saw it. If it has, merge your update into the current version rather than replacing it wholesale, the same way you'd resolve a merge conflict. This applies in both directions: Claude Code should assume Cowork may have added a decision or competitive note since the last local read, and vice versa.

## Pull Request Workflow (2026-09-20)
Follow this every time a PR is opened for this repo:

1. **Start clean.** Branch from an up-to-date `main` (`git checkout main && git pull --ff-only`, then `git checkout -b <name>`). Commit only your own files — never `.claude/`, `docs/Smiley_xml_*.xml` or `src/SmilrApi.Api/wwwroot/launch.html`, and don't stage `CLAUDE.md` or `docs/business-opportunities.md` without re-reading them first (see Multi-Session Editing).
2. **Open the PR, don't merge it.** Push and `gh pr create --base main`. The user merges it (`gh pr merge` is blocked in auto mode); merging to `main` deploys to production, so the PR description should say what was and wasn't tested.
3. **Report local state on request or after a merge.** When the user asks whether local is up to date, or says a PR merged: `git fetch --prune`, check `gh pr view <n> --json state`, and report the current branch vs its remote and local `main` vs `origin/main` (ahead/behind).
4. **After the PR shows `MERGED`, clean up — standing permission, no need to ask again:** `git checkout main`, `git pull --ff-only`, `git branch -d <branch>` (lowercase `-d`, which refuses to delete an unmerged branch), `git push origin --delete <branch>` for the branch created for that PR, then `git fetch --prune`. Finish by reporting the resulting state.
5. **Never delete more than that.** If the PR isn't `MERGED` yet, delete nothing and say so. Leave alone every branch not created for that PR (e.g. `customer-domain-test` and its worktree `C:\Projects\Smilr-customer-domain-test`, other merged remote branches), the stash, and untracked files — mention them instead.
