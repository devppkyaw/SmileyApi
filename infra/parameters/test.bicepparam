using '../main.bicep'

// Isolated test environment for the customer-domain end-to-end test project.
// See docs/CustomerDomain-TestPlan.md for the full plan and execution order.
// Deployed manually (az deployment group create), NOT wired into deploy.yml's
// auto-deploy-on-push-to-main — this environment is provisioned/updated on demand.

param appName = 'smilr-api'
param environment = 'test'
param location = 'northeurope'
param sqlAdminLogin = 'smilradmin'
// Same pattern as infra/parameters/prod.bicepparam: the installed Bicep CLI version here
// doesn't support layering --parameters overrides on top of a .bicepparam file, so these
// read from env vars at deploy time instead:
//   SQL_ADMIN_PASSWORD=... IMAGE_NAME=... az deployment group create --resource-group rg-smilr-test \
//     --template-file infra/main.bicep --parameters infra/parameters/test.bicepparam
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param imageName = readEnvironmentVariable('IMAGE_NAME')

// Cost settings (docs/azure-cost-reduction-brief.md). In September 2026 this mostly idle environment
// cost as much as prod, because a replica, the sync worker and Hangfire's queue polling ran 24/7.
// - Scale to zero when idle; the first request after idling pays a cold start.
// - Smallest Container Apps size.
// - No background work: no daily XML sync, no Hangfire server. Trigger a sync by hand with
//   POST /admin/sync (X-Admin-Key header) when test needs fresh data.
// - Hard 0.1 GB/day ingestion cap on the workspace.
param minReplicas = 0
param containerCpu = '0.25'
param containerMemory = '0.5Gi'
param backgroundJobsEnabled = false
param logDailyQuotaGb = '0.1'

// /admin/sync and the other /admin/* endpoints run UNAUTHENTICATED when Admin--Key is empty. Pass
// ADMIN_KEY at deploy time to (re)provision it in this environment's Key Vault; leaving it unset
// skips the secret rather than clearing an existing one.
param adminKey = readEnvironmentVariable('ADMIN_KEY', '')

// Stripe: intentionally NOT set here yet. Per docs/CustomerDomain-TestPlan.md, this test
// environment will be wired to Smilr's one real *live* Stripe account (not a throwaway
// test-mode account) once that account is activated (user action, step 5 in the plan).
// Add stripeSecretKey / stripeWebhookSecret / stripePriceId here (or set directly in
// Key Vault, same caveat as emailOverrideAddress below) once that's ready.

// Email: point test emails at a distinct sender/override so they're distinguishable from
// prod. Reusing the existing ACS resource (acs-smilr-api-prod-4lubrf) rather than
// provisioning a new one — TODO confirm no custom-domain binding conflict once ACS module
// wiring for a shared resource is worked out (see plan doc, item 5).
// param emailOverrideAddress = 'system+test@smilrhq.dk'

// customDomainName intentionally left unset until the test .dk domain is registered and
// its DNS records (TXT asuid.<domain> + apex CNAME) are live — see plan doc steps 1-2 and
// the two-phase deploy pattern documented in docs/CustomerDomain-TestPlan.md /
// project_smilr_domain_golive. Uncomment and deploy in two phases once ready:
// param customDomainName = '<test-domain>.dk'
// param provisionManagedCertificate = false   // phase 1, then true on the follow-up deploy
