param name string
param location string
param sqlServerFqdn string
param sqlDatabaseName string
param sqlAdminLogin string

@secure()
param sqlAdminPassword string

@secure()
param acsConnectionString string

param acsSenderAddress string
param emailOverrideAddress string = ''

@secure()
param stripeSecretKey string = ''

@secure()
param stripeWebhookSecret string = ''

param stripePriceId string = ''

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

// Acs--ConnectionString and Acs--SenderAddress map to Acs:ConnectionString and Acs:SenderAddress
// in ASP.NET Core config (-- separator → : hierarchy).
resource acsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Acs--ConnectionString'
  properties: {
    value: acsConnectionString
  }
}

resource acsSenderAddressSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Acs--SenderAddress'
  properties: {
    value: acsSenderAddress
  }
}

resource emailOverrideSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(emailOverrideAddress)) {
  parent: vault
  name: 'Email--OverrideAddress'
  properties: {
    value: emailOverrideAddress
  }
}

resource stripeSecretKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Stripe--SecretKey'
  properties: {
    value: stripeSecretKey
  }
}

resource stripeWebhookSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Stripe--WebhookSecret'
  properties: {
    value: stripeWebhookSecret
  }
}

resource stripePriceIdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Stripe--PriceId'
  properties: {
    value: stripePriceId
  }
}

output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri
output vaultId string = vault.id
