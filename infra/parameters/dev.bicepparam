using '../main.bicep'

param appName = 'smilr-api'
param environment = 'dev'
param location = 'westeurope'
param sqlAdminLogin = 'smilradmin'
// sqlAdminPassword is not set here — pass at deploy time:
// az deployment group create ... --parameters sqlAdminPassword=$SQL_PASSWORD
