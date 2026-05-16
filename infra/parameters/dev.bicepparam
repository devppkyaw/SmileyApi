using '../main.bicep'

param appName = 'smiley-api'
param environment = 'dev'
param location = 'westeurope'
param sqlAdminLogin = 'smileyadmin'
// sqlAdminPassword is not set here — pass at deploy time:
// az deployment group create ... --parameters sqlAdminPassword=$SQL_PASSWORD
