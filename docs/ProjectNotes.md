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
| `Email--OverrideAddress` | `devppkyaw@gmail.com` | Dev/test redirect — set manually, not managed by Bicep |
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

## Azure Resources

| Resource | Name |
|---|---|
| Container App | `ca-smilr-api-prod-4lubrf` |
| Resource Group | `rg-smiley-api-prod` |
| Key Vault | `kv-smilr-api-prod-4lubrf` |
| ACS | `acs-smilr-api-prod-4lubrf` |
| Live URL | `https://ca-smilr-api-prod-4lubrf.ashysand-2878a2b9.northeurope.azurecontainerapps.io/` |
