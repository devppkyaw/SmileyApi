param name string
param location string
param sqlServerFqdn string
param sqlDatabaseName string
param sqlAdminLogin string

@secure()
param sqlAdminPassword string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
  }
}

// The -- separator maps to : in the ASP.NET Core config hierarchy,
// so this resolves to ConnectionStrings:Default at runtime.
resource connStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'ConnectionStrings--Default'
  properties: {
    value: 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri
output vaultId string = vault.id
