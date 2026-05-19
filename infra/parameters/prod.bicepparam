using '../main.bicep'

param appName = 'smiley-api'
param environment = 'prod'
param location = 'northeurope'
param sqlAdminLogin = 'smileyadmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param imageName = readEnvironmentVariable('IMAGE_NAME')
param ghcrUsername = readEnvironmentVariable('GHCR_USERNAME')
param ghcrPassword = readEnvironmentVariable('GHCR_TOKEN')
