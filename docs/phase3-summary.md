# Phase 3 Summary — Azure Infrastructure (Bicep IaC)

**Date:** 2026-05-16  
**Branch:** main  

---

## What Is Bicep and What Does It Do Here

Bicep is Azure's infrastructure-as-code language. Instead of clicking through the Azure portal to create resources, you describe what you want in `.bicep` files and Azure builds it. The entire Smiley API environment — database, hosting, secrets, telemetry — is defined in `infra/` and can be created or recreated with one command.

When you run `az deployment group create ...`, Azure reads the Bicep and provisions these resources in order:

1. **Log Analytics + Application Insights** (`monitoring.bicep`) — telemetry sink. Your API's request logs, exceptions, and performance data flow here. You can query them in the Azure portal.

2. **SQL Server + Database** (`sql.bicep`) — replaces your local `localhost\SQLEXPRESS`. Same SQL Server engine, hosted by Azure. The database is named `SmileyApi`, same as local. Basic 5 DTU = ~$5/mo, 2 GB — enough for 57k establishments + inspection history.

3. **Key Vault + connection string secret** (`keyvault.bicep`) — stores the Azure SQL connection string as a secret named `ConnectionStrings--Default`. Your app reads it from here at startup instead of `appsettings.json`. The SQL password never sits in a file or environment variable.

4. **App Service Plan + App Service** (`appservice.bicep`) — the Linux host that runs your ASP.NET Core process. This hosts both the API *and* the `XmlSyncWorker` background service in the same process, exactly like `dotnet run` locally, just on Azure infrastructure. Currently F1 (Free) — upgrade to B1 (~$13/mo) when needed.

5. **Key Vault role assignment** (`main.bicep`) — gives the App Service permission to read from the Key Vault using its **Managed Identity** (an automatically-managed Azure AD identity). No credentials stored anywhere — the App Service proves who it is to Key Vault cryptographically.

**The flow at runtime:**

```
App starts on Azure
  → reads AZURE_KEY_VAULT_URI from App Service config
  → fetches ConnectionStrings--Default secret from Key Vault (via Managed Identity — no password needed)
  → connects to Azure SQL using that connection string
  → XmlSyncWorker runs every 24h, bulk-upserts into Azure SQL (same SqlBulkCopy + MERGE path as local)
  → all request traces and exceptions stream to Application Insights
```

---

## What Was Built

Phase 3 introduces the complete Azure infrastructure definition as Bicep IaC. Running a single `az deployment group create` command provisions every cloud resource the Smiley API needs — SQL database, app hosting, secrets storage, and telemetry — in a reproducible, version-controlled way.

---

## Files Changed

### New Files

| File | Purpose |
|---|---|
| `infra/main.bicep` | Orchestrator — wires all modules together and creates the Key Vault role assignment |
| `infra/modules/monitoring.bicep` | Log Analytics workspace + Application Insights |
| `infra/modules/sql.bicep` | Azure SQL Server + Basic database + Azure-services firewall rule |
| `infra/modules/keyvault.bicep` | Key Vault + connection string secret |
| `infra/modules/appservice.bicep` | App Service Plan (B1 Linux) + App Service with system-assigned Managed Identity |
| `infra/parameters/dev.bicepparam` | Non-secret parameter values for the `dev` environment |
| `infra/parameters/prod.bicepparam` | Non-secret parameter values for the `prod` environment |

---

## Architecture

```
az deployment group create (one command)
  │
  ├── monitoring.bicep
  │     ├── Log Analytics workspace (log-smiley-api-prod-xxxxxx)
  │     └── Application Insights    (appi-smiley-api-prod-xxxxxx)
  │
  ├── sql.bicep
  │     ├── SQL Server              (sql-smiley-api-prod-xxxxxx.database.windows.net)
  │     ├── Database                (SmileyApi, Basic 5 DTU, 2 GB)
  │     └── Firewall rule           (allow Azure-internal traffic only)
  │
  ├── keyvault.bicep
  │     ├── Key Vault               (kv-smiley-api-prod-xx, RBAC mode)
  │     └── Secret: ConnectionStrings--Default  (full Azure SQL connection string)
  │
  ├── appservice.bicep
  │     ├── App Service Plan        (asp-smiley-api-prod-xxxxxx, B1 Linux ~$13/mo)
  │     └── App Service             (app-smiley-api-prod-xxxxxx.azurewebsites.net)
  │           ├── System-assigned Managed Identity
  │           ├── ASPNETCORE_ENVIRONMENT = Production
  │           ├── AZURE_KEY_VAULT_URI    = https://kv-...vault.azure.net/
  │           └── APPLICATIONINSIGHTS_CONNECTION_STRING = InstrumentationKey=...
  │
  └── main.bicep (role assignment)
        └── Key Vault Secrets User role → App Service Managed Identity
```

**Runtime flow after deploy:**

```
App starts on Azure App Service
  → reads AZURE_KEY_VAULT_URI from app settings
  → fetches secret ConnectionStrings--Default from Key Vault (via Managed Identity, no password needed)
  → connects to Azure SQL using that connection string
  → XmlSyncWorker runs on 24h cycle, bulk-upserts into Azure SQL (same SqlBulkCopy + MERGE path as local)
  → all request traces and exceptions stream to Application Insights
```

---

## Key Decisions

**Globally unique resource names via `uniqueString(resourceGroup().id)`.**  
App Service names form `*.azurewebsites.net` hostnames and must be globally unique. Key Vault names are also globally unique and capped at 24 characters. A 6-character suffix derived from the resource group ID is appended to all resource names. The suffix is deterministic per-RG, so re-running the deployment produces the same names and updates in place rather than creating duplicates. `take('kv-${suffix}', 24)` ensures the vault name never exceeds the limit.

**Role assignment in `main.bicep` avoids a circular module dependency.**  
Granting the App Service's Managed Identity access to Key Vault requires both the vault ID (from `keyvault` module) and the App Service's principal ID (from `appservice` module). Putting the role assignment inside either module would create a circular output dependency. It lives in `main.bicep` as a top-level resource, which Bicep resolves naturally after both modules complete.

**Connection string is constructed and stored inside `keyvault.bicep` — never surfaced as a module output.**  
Bicep module outputs appear in the ARM deployment history in plain text. Building the connection string from `@secure()` parameters inside the module and writing it directly to the vault secret means the full credential never appears in any output or log. The SQL password is always a `@secure()` parameter — it is not committed to any parameter file.

**`enableRbacAuthorization: true` on the Key Vault.**  
Older Key Vault access policies are per-vault configuration objects that are easy to forget, hard to audit, and cannot be managed with standard Azure RBAC tooling. RBAC mode treats vault access like any other Azure resource — role assignments visible in IAM, revocable at the subscription level, compatible with `az role assignment` and GitHub OIDC federation. The `Key Vault Secrets User` built-in role (`4633458b-...`) grants read-only secret access, nothing else.

**App Service, not Container Apps.**  
`XmlSyncWorker` is a `BackgroundService` hosted in the same ASP.NET Core process as the API. App Service runs the published `dotnet` binary directly with no containerisation required. Container Apps would add Docker build steps, a container registry, and image management for no benefit at this scale. `WEBSITE_RUN_FROM_PACKAGE=1` makes deployments atomic — the app restarts from a zip rather than from a directory of individually-replaced files.

**Basic 5 DTU SQL tier (~$5/mo).**  
The workload is a periodic background sync (57k rows once per 24h via `SqlBulkCopy` + `MERGE`) plus low-traffic API reads. Basic provides 2 GB storage and 5 DTUs — sufficient for this data volume. Scaling to Standard S0 (10 DTUs, ~$15/mo) or higher is a one-command operation and does not require any application changes.

**`softDeleteRetentionInDays: 7` on Key Vault.**  
Azure requires soft-delete on new vaults. Seven days is the minimum allowed. This means accidentally deleted secrets can be recovered within a week, and accidental vault deletion does not immediately destroy the connection string.

---

## Azure Resources Provisioned

| Resource | Name pattern | SKU / Tier | Est. monthly cost |
|---|---|---|---|
| Log Analytics workspace | `log-smiley-api-{env}-{suffix}` | Pay-as-you-go (first 5 GB/mo free) | ~$0 |
| Application Insights | `appi-smiley-api-{env}-{suffix}` | Workspace-based | ~$0 |
| SQL Server | `sql-smiley-api-{env}-{suffix}` | — | — |
| SQL Database | `SmileyApi` | Basic, 5 DTU, 2 GB | ~$5 |
| Key Vault | `kv-smiley-api-{env}-{suffix}` (max 24 chars) | Standard | ~$0 |
| App Service Plan | `asp-smiley-api-{env}-{suffix}` | F1 Free (upgrade to B1 when ready) | $0 ($13 on B1) |
| App Service | `app-smiley-api-{env}-{suffix}` | — (on F1 plan) | included |

**Total estimated: ~$5/mo (F1 free tier) — ~$18/mo after upgrading to B1.**

---

## One-Time Setup Guide

Everything below runs once — after this, every push to `main` deploys automatically via GitHub Actions.

**Prerequisites:**
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [GitHub CLI](https://cli.github.com/) installed (optional, but makes secret setup one command)
- An Azure subscription
- The repo pushed to GitHub

---

### A — Authenticate with Azure
*`az login` + pick the right subscription*

```bash
az login

# If you have multiple subscriptions, pick the right one:
az account list --output table
az account set --subscription "<subscription-id>"
```

---

### B — Deploy all Azure resources
*`az group create` + `az deployment group create` — provisions all 7 Azure resources in one shot*

Run from the repo root. The deployment takes 3–5 minutes.

```powershell
az group create --name rg-smiley-api-prod --location northeurope

$env:SQL_ADMIN_PASSWORD = "<your-strong-password>"
az deployment group create `
  --resource-group rg-smiley-api-prod `
  --template-file infra/main.bicep `
  --parameters infra/parameters/prod.bicepparam
```

SQL password rules: minimum 8 characters, must contain uppercase, lowercase, digit, and a special character (e.g. `MyP@ssw0rd!`). The password is passed via the `SQL_ADMIN_PASSWORD` environment variable — it is never committed to any file and lives only in Key Vault after this command runs.

---

### C — Capture the deployment outputs
*Read the Bicep outputs to get the App Service name and hostname*

```bash
az deployment group show \
  --resource-group rg-smiley-api-prod \
  --name main \
  --query "properties.outputs" \
  --output table
```

Note these four values — you will need them in the next steps:

| Output | Example value |
|---|---|
| `appServiceName` | `app-smiley-api-prod-abc123` |
| `appServiceHostname` | `app-smiley-api-prod-abc123.azurewebsites.net` |
| `keyVaultName` | `kv-smiley-api-prod-ab` |
| `sqlServerName` | `sql-smiley-api-prod-abc123` |

---

### D — Download the App Service publish profile
*Downloads the credential XML that lets GitHub Actions deploy to your App Service*

```bash
az webapp deployment list-publishing-profiles \
  --resource-group rg-smiley-api-prod \
  --name <appServiceName> \
  --xml
```

Copy the entire XML output (starts with `<publishData>`, ends with `</publishData>`).

---

### E — Set the GitHub repo variable and secret
*Wires up GitHub Actions with the App Service name and publish profile*

**Using GitHub CLI (recommended):**

```bash
# Non-sensitive: stored as a repo variable
gh variable set AZURE_WEBAPP_NAME --body "app-smiley-api-prod-abc123"

# Sensitive: stored as an encrypted repo secret
# Paste the XML from Step D when prompted, then press Ctrl+D
gh secret set AZURE_WEBAPP_PUBLISH_PROFILE
```

**Or via GitHub UI:**
- Variables: repository → Settings → Secrets and variables → Actions → Variables → New repository variable
- Secrets: repository → Settings → Secrets and variables → Actions → Secrets → New repository secret

---

### F — Trigger the first deploy
*`git push origin main` — kicks off the GitHub Actions deploy pipeline*

```bash
git push origin main
```

The `deploy.yml` workflow triggers automatically. Watch it at:
`https://github.com/<your-org>/<your-repo>/actions`

The first run takes longer than usual because EF's `MigrateAsync()` runs on startup and creates the full schema on the empty Azure SQL database.

---

### G — Verify
*`curl /health` — confirms the app is live, connected to Azure SQL, and reading from Key Vault*

```bash
curl https://<appServiceHostname>/health
# Expected: {"status":"ok"}
```

If the health check returns 200, the app is running, connected to Azure SQL, and secrets are loading correctly from Key Vault.

---

## What Is Not Included (Post-MVP)

| Item | Note |
|---|---|
| Custom domain + managed TLS certificate | App Service has a free managed cert — add via portal once you have a domain |
| `/admin/keys` in production | Disabled by `IsProduction()` gate — create keys via local dev or a protected admin tool |

---

## Milestone Status

> **Phase 3 Step 1 — Azure infrastructure (Bicep IaC)** ✅  
> **Phase 3 Step 2 — App code changes (Key Vault, AppInsights, migrations, prod gates)** ✅  
> **Phase 3 Step 3 — GitHub Actions CI/CD** ✅  
> **Phase 3 Step 4 — One-time setup guide** ✅
