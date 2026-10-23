using '../main.bicep'

param appName = 'smilr-api'
param environment = 'prod'
param location = 'northeurope'
param sqlAdminLogin = 'smilradmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param imageName = readEnvironmentVariable('IMAGE_NAME')
param emailSystemMonitorAddress = 'system@smilrhq.dk'

// smilrhq.dk DNS records (TXT asuid.smilrhq.dk + apex CNAME to the Container App, DNS-only mode)
// were added at Cloudflare and confirmed propagated on 2026-08-23.
param customDomainName = 'smilrhq.dk'

// Two-phase deploy required — see provisionManagedCertificate's description in main.bicep and the
// comments in infra/modules/containerapps.bicep for why (Azure requires the hostname registered on the
// container app before a managed cert can be created for it). Phase 1 (customDomainName set, this false)
// deployed successfully on 2026-08-23 — hostname is registered, confirmed reachable over plain HTTP.
// Phase 2 (this true): create the managed cert and switch the binding to HTTPS.
param provisionManagedCertificate = true

// Cost settings (docs/azure-cost-reduction-brief.md). Replica count and size are unchanged on purpose:
// - minReplicas stays 1 because the daily XML sync and Hangfire webhook delivery run in-process. Scaling
//   prod to zero needs them moved out first (Container Apps Job — a separate, later change).
// - 0.5 vCPU / 1Gi: the replica's working set sits around 370 MB, too close to 0.5Gi's limit to risk
//   during the daily XML parse.
// The daily cap leaves ~5x headroom over the expected ~0.1 GB/day once Hangfire's queue polling stopped
// being logged as SQL dependencies. If the cap is hit, telemetry stops until midnight UTC — raise it
// rather than remove it.
param minReplicas = 1
param containerCpu = '0.5'
param containerMemory = '1Gi'
param backgroundJobsEnabled = true
param logDailyQuotaGb = '0.5'
