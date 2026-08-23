using '../main.bicep'

param appName = 'smilr-api'
param environment = 'prod'
param location = 'northeurope'
param sqlAdminLogin = 'smilradmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param imageName = readEnvironmentVariable('IMAGE_NAME')
param emailSystemMonitorAddress = 'system@smilrhq.dk'

// smilrhq.dk DNS records (TXT asuid.smilrhq.dk + apex CNAME to the Container App, DNS-only mode)
// were added at Cloudflare and confirmed propagated on 2026-08-23. This deploy binds the domain and
// provisions the managed TLS cert.
param customDomainName = 'smilrhq.dk'
