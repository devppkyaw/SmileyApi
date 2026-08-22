using '../main.bicep'

param appName = 'smilr-api'
param environment = 'prod'
param location = 'northeurope'
param sqlAdminLogin = 'smilradmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param imageName = readEnvironmentVariable('IMAGE_NAME')
param emailSystemMonitorAddress = 'system@smilrhq.dk'

// customDomainName is intentionally not set here yet. smilrhq.dk currently points at an unrelated
// static site (Cloudflare) — do not set this until DNS/ownership validation records have been added
// externally. Once ready: set customDomainName = 'smilrhq.dk', redeploy, then add the Azure-provided
// CNAME/TXT validation record at the DNS provider, then redeploy again to complete cert issuance.
