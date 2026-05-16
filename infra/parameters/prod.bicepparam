using '../main.bicep'

param appName = 'smiley-api'
param environment = 'prod'
param location = 'westeurope'
param sqlAdminLogin = 'smileyadmin'
// sqlAdminPassword is not set here — inject via GitHub secret SQL_ADMIN_PASSWORD at deploy time.
