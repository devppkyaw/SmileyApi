@description('Base name for all resources. E.g. "smilr-api"')
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@allowed(['dev', 'prod'])
param environment string

@description('SQL Server administrator login.')
param sqlAdminLogin string

@secure()
@description('SQL Server administrator password. Min 8 chars, must contain upper, lower, digit, special.')
param sqlAdminPassword string

@description('Full ghcr.io image reference, e.g. ghcr.io/owner/smilr-api:sha-abc1234')
param imageName string


// A short deterministic suffix scoped to this resource group avoids global naming conflicts
// for App Service (*.azurewebsites.net) and Key Vault (globally unique).
var uniqueSuffix = take(uniqueString(resourceGroup().id), 6)
var suffix = '${appName}-${environment}-${uniqueSuffix}'
var kvName = take('kv-${suffix}', 24)
var sqlDatabaseName = 'SmilrApi'

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    name: suffix
    location: location
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    name: suffix
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    databaseName: sqlDatabaseName
  }
}

module acs 'modules/acs.bicep' = {
  name: 'acs'
  params: {
    name: suffix
    location: location
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: kvName
    location: location
    sqlServerFqdn: sql.outputs.serverFqdn
    sqlDatabaseName: sqlDatabaseName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    acsConnectionString: acs.outputs.connectionString
    acsSenderAddress: acs.outputs.senderAddress
  }
}

module containerApp 'modules/containerapps.bicep' = {
  name: 'containerapps'
  params: {
    name: suffix
    location: location
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    keyVaultUri: keyvault.outputs.vaultUri
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    environment: environment
    imageName: imageName
  }
}

// Grant the Container App's system-assigned identity the Key Vault Secrets User role.
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource kvRef 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: kvName
}

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, kvName, kvSecretsUserRoleId)
  scope: kvRef
  properties: {
    roleDefinitionId: '/providers/Microsoft.Authorization/roleDefinitions/${kvSecretsUserRoleId}'
    principalId: containerApp.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

output containerAppHostname string = containerApp.outputs.hostname
output keyVaultName string = keyvault.outputs.vaultName
output sqlServerName string = sql.outputs.serverName
