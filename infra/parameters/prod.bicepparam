using '../main.bicep'

param appName = 'smilr-api'
param environment = 'prod'
param location = 'northeurope'
param sqlAdminLogin = 'smilradmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param imageName = readEnvironmentVariable('IMAGE_NAME')
