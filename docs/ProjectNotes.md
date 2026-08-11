# Project Notes

## Key Vault Secrets

Key Vault: `kv-smilr-api-prod-4lubrf`

Secrets are loaded once at startup via `AddAzureKeyVault` in `Program.cs`. After updating a secret, force a new container revision to pick it up:

```powershell
az containerapp update --name ca-smilr-api-prod-4lubrf --resource-group rg-smiley-api-prod --set-env-vars "RESTART_TRIGGER=1"
az containerapp update --name ca-smilr-api-prod-4lubrf --resource-group rg-smiley-api-prod --remove-env-vars "RESTART_TRIGGER"
```

| Secret | Value | Notes |
|---|---|---|
| `ConnectionStrings--Default` | Set by Bicep | SQL connection string, auto-generated |
| `Acs--ConnectionString` | Set by Bicep | Azure Communication Services |
| `Acs--SenderAddress` | Set by Bicep | `donotreply@3e9d7845-...azurecomm.net` |
| `Email--OverrideAddress` | `system@smilrhq.dk` | Dev/test redirect — set manually, not managed by Bicep. Changed 2026-08-11 (was `devppkyaw@gmail.com`) |
| `Stripe--SecretKey` | **Not set** | Get from Stripe Dashboard → Developers → API keys |
| `Stripe--WebhookSecret` | **Not set** | Get from Stripe Dashboard after creating webhook endpoint |
| `Stripe--PriceId` | **Not set** | Get from Stripe Dashboard → Products |

To set a secret manually:
```powershell
az keyvault secret set --vault-name kv-smilr-api-prod-4lubrf --name <secret-name> --value "<value>"
```

## Stripe Webhook Endpoint

- URL: `https://ca-smilr-api-prod-4lubrf.ashysand-2878a2b9.northeurope.azurecontainerapps.io/v1/stripe/webhook`
- Events: `checkout.session.completed`, `customer.subscription.deleted`
- In Stripe Dashboard: Developers → Webhooks → Add destination → Webhook

## Data Sync (Fødevarestyrelsen)

- **Source:** `https://www.foedevarestyrelsen.dk/Media/638212360788086849/Smiley_xml.xml` — plain XML, no ZIP (see `FodevareXmlParser.cs`)
- **Automatic:** `XmlSyncWorker` (background service) runs once on app startup, then every 24h. Skipped in production if `Establishments` data is already <20h old, to avoid a redundant resync on every container restart.
- **Manual:** `POST /admin/sync`
  - Header: `X-Admin-Key` (checked against the `Admin--Key` secret above; unauthenticated if that secret is unset)
  - UI: `/admin/sync.html` — "Force XML Sync" page, paste the admin key and click to trigger
  - Runs the exact same full-resync logic as the scheduled worker, but **bypasses the 20h freshness check** — always does a real sync
  - Responses: `202 {"status":"started"}` (fire-and-forget, runs in background), `409 {"status":"already_running"}` if one's already in flight, `401` on bad/missing key

## Azure Resources

| Resource | Name |
|---|---|
| Container App | `ca-smilr-api-prod-4lubrf` |
| Resource Group | `rg-smiley-api-prod` |
| Key Vault | `kv-smilr-api-prod-4lubrf` |
| ACS | `acs-smilr-api-prod-4lubrf` |
| Live URL | `https://ca-smilr-api-prod-4lubrf.ashysand-2878a2b9.northeurope.azurecontainerapps.io/` |
