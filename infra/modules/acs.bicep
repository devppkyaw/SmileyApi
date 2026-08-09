param name string
param location string

// ACS Communication + Email services are global resources; data residency is set via dataLocation.
resource communication 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-${name}'
  location: 'global'
  properties: {
    dataLocation: 'Europe'
    // TODO: once notify.smilrhq.dk DNS records are verified in Azure Portal,
    // change to: linkedDomains: [customDomain.id]
    linkedDomains: [azureManagedDomain.id]
  }
}

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: 'email-${name}'
  location: 'global'
  properties: {
    dataLocation: 'Europe'
  }
}

resource azureManagedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

// Custom domain — using a subdomain (not the apex smilrhq.dk) so ACS's SPF record
// never has to coexist with the apex domain's Google Workspace SPF record.
// After adding DNS records at your registrar and verifying in the portal:
//   1. Change linkedDomains above to: [customDomain.id]
//   2. Change senderAddress output below to: 'donotreply@notify.smilrhq.dk'
//   3. Redeploy (push to main).
resource customDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'notify.smilrhq.dk'
  location: 'global'
  properties: {
    domainManagement: 'CustomerManaged'
  }
}

@description('ACS connection string — store in Key Vault, never in plain config.')
@secure()
output connectionString string = communication.listKeys().primaryConnectionString

// TODO: once notify.smilrhq.dk is verified, change to: 'donotreply@notify.smilrhq.dk'
output senderAddress string = 'donotreply@${azureManagedDomain.properties.mailFromSenderDomain}'
