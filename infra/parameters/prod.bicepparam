using '../main.bicep'

param appName = 'smiley-api'
param environment = 'prod'
param location = 'uksouth'
param sqlAdminLogin = 'smileyadmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
