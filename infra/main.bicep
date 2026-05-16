@description('Base name for all resources. E.g. "smiley-api"')
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

// A short deterministic suffix scoped to this resource group avoids global naming conflicts
// for App Service (*.azurewebsites.net) and Key Vault (globally unique).
var uniqueSuffix = take(uniqueString(resourceGroup().id), 6)
var suffix = '${appName}-${environment}-${uniqueSuffix}'
var kvName = take('kv-${suffix}', 24)
var sqlDatabaseName = 'SmileyApi'

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

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: kvName
    location: location
    sqlServerFqdn: sql.outputs.serverFqdn
    sqlDatabaseName: sqlDatabaseName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
  }
}

module appservice 'modules/appservice.bicep' = {
  name: 'appservice'
  params: {
    name: suffix
    location: location
    keyVaultUri: keyvault.outputs.vaultUri
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    environment: environment
  }
}

// Grant the App Service's system-assigned identity the Key Vault Secrets User role.
// This lets the app read secrets without any stored credentials.
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e0'

resource kvRef 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: kvName
}

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvRef.id, appservice.outputs.principalId, kvSecretsUserRoleId)
  scope: kvRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: appservice.outputs.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [keyvault]
}

output appServiceHostname string = appservice.outputs.hostname
output appServiceName string = appservice.outputs.appServiceName
output keyVaultName string = keyvault.outputs.vaultName
output sqlServerName string = sql.outputs.serverName
