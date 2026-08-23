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
