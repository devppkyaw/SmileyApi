# Azure cost reduction — implementation brief

**Goal:** bring the monthly Azure bill from ~DKK 920 (September 2026) down to ~DKK 150–300, without breaking production.

## Context: September 2026 cost by resource

| Resource | Prod (rg-smiley-api-prod) | Test (rg-smilr-test) |
|---|---|---|
| Log Analytics workspace | 238.68 | 228.21 |
| Container App | 199.71 | 192.58 |
| SQL Basic | 31.00 | 31.00 |

Test costs as much as prod even though it gets almost no traffic. So the cost is driven by **background activity running around the clock**, not by users:
- Both Container Apps appear to run a replica 24/7 (min replicas ≥ 1, and/or the in-process sync worker and Hangfire server keep it busy).
- Both Log Analytics workspaces ingest similar volume. The suspected sources are Hangfire polling SQL (logged as dependencies and traces), framework logging at Information level, and Container Apps console logs duplicating App Insights.
- `smilr-overview.md` says the "skip sync if data is less than 20 hours old" rule applies in **production only**. So test likely re-runs the full XML sync (30k+ records) on every restart or new revision.

The test Container App has been stopped manually in the portal, and a daily cap set on the test workspace, as an immediate measure. A Bicep redeploy may undo that. The changes below make it permanent.

---

## Step 0: Investigate first, report back before changing anything

Confirm each of these in the code and write down what you find:
1. **Bicep `containerapps` module:** current `minReplicas`, `maxReplicas`, CPU and memory, scale rules, and how the environment's log destination is configured (Log Analytics vs. none). Is anything parameterized per environment?
2. **Bicep Log Analytics / App Insights:** SKU, retention, whether `dailyQuotaGb` (workspace capping) is set, and whether App Insights is workspace-based.
3. **Telemetry SDK:** classic `Microsoft.ApplicationInsights.AspNetCore`, or Azure Monitor OpenTelemetry (`Azure.Monitor.OpenTelemetry.AspNetCore`)? Is sampling configured? Is SQL dependency tracking on?
4. **Hangfire:** storage options (`QueuePollInterval`, `SchedulePollingInterval`, `JobExpirationCheckInterval`, etc.), number of workers, and whether the server starts unconditionally in every environment.
5. **Sync worker:** where the "less than 20 hours old" skip is implemented, and how it decides it's in production.
6. **Logging:** current `Logging:LogLevel` in `appsettings*.json`, and any per-record `LogInformation` calls inside the sync / XML parse / MERGE path.

If you can run queries against the workspaces, run this in both and include the result:
```kusto
Usage
| where TimeGenerated > ago(30d)
| where IsBillable == true
| summarize GB = sum(Quantity) / 1000 by DataType
| sort by GB desc
```

---

## Step 1: Infrastructure (Bicep), per-environment parameters

- Parameterize `minReplicas` per environment:
  - **test: 0.**
  - **prod: leave unchanged for now.** The sync worker and Hangfire server run in-process, so scaling prod to zero would stop the daily sync and webhook delivery. See Step 4.
- Parameterize CPU and memory per environment. Use the smallest size that works (e.g. 0.25 vCPU / 0.5 Gi) unless Step 0 shows prod needs more.
- Log Analytics workspaces:
  - `workspaceCapping.dailyQuotaGb`: **test 0.1**, **prod 0.5** (adjust after the Usage query).
  - Retention: 30 days.
- Container Apps environment log destination: if App Insights already captures the app's logs, avoid storing the same lines twice. Either stop streaming console logs into Log Analytics, or keep only system logs. Explain the trade-off you choose.
- Make sure a redeploy from GitHub Actions **does not undo these settings**.

## Step 2: Application logging and telemetry

- `appsettings.json` / `appsettings.Production.json`:
  - Default log level `Warning`; keep `Information` only for Smilr's own categories where it's actually useful (e.g. one summary line per sync run).
  - `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `Hangfire`, `System.Net.Http`: `Warning`.
- Replace any per-record logging in the sync path with a single summary per run (records parsed, inserted, updated, score changes detected, duration).
- Application Insights:
  - Enable sampling (fixed-rate or adaptive, whichever fits the SDK in use).
  - Filter out Hangfire's background SQL polling dependencies. Use a telemetry processor or filter for the classic SDK, or a processor or SqlClient instrumentation filter for OpenTelemetry. Keep the dependencies from real requests and the sync MERGE.
  - Exclude health-probe requests from request telemetry if probes are configured.

## Step 3: Background work per environment

- Add a config flag, e.g. `BackgroundJobs:Enabled` (default `true`; **`false` in test**). When it's false:
  - Don't start the Hangfire server (the dashboard and enqueueing can stay if needed).
  - Don't start the scheduled XML sync worker.
- Provide a way to trigger a sync manually in test when needed (an admin-only endpoint, or a CLI/one-off run). Keep it protected.
- Apply the "skip if data is less than 20 hours old" rule in **all** environments, configurable (e.g. `Sync:MinHoursBetweenRuns`), instead of production only.
- Hangfire polling: jobs here run roughly daily (sync and webhook fan-out), so raise the polling intervals substantially (e.g. queue and schedule polling around 1 minute), unless Step 0 shows a reason not to. Note how this affects webhook delivery latency.

## Step 4 (separate PR, propose first): let prod scale to zero

Write a short proposal before implementing. Don't build it in the same PR as Steps 1–3.
- Move the daily XML sync into an **Azure Container Apps Job** with a cron schedule, using the same image with a different entry point or argument.
- Decide where Hangfire webhook jobs get processed when the web app is scaled to zero. Options: run a Hangfire server inside the job for the duration of the sync and fan-out, or keep a small always-on worker. Describe the trade-offs (cost, webhook latency, retries).
- Then set prod `minReplicas: 0` and note the cold-start impact on the API, widget and directory.

---

## Constraints

- Work on a branch, e.g. `azure-cost-reduction`. Steps 1–3 in one PR; Step 4 separately.
- No functional change to production behaviour from Steps 1–3, other than less logging and telemetry.
- Don't delete any resources or data (including the test SQL database).
- Update the cost estimate and the sync-skip description in `docs/smilr-overview.md` to match the new reality.

## How to verify

- After deploy, in each workspace, the Usage query over the last 24h should show a large drop in `AppDependencies` / `AppTraces` / console log tables.
- The test Container App shows 0 replicas when idle.
- Prod: the daily sync still runs, score-change webhooks still deliver, and `/find` pages and the API respond normally.
- After about a week, check Cost Management → Cost analysis, grouped by Resource, against the September numbers above.
