param name string
param location string

// ACS Communication + Email services are global resources; data residency is set via dataLocation.
resource communication 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-${name}'
  location: 'global'
  properties: {
    dataLocation: 'Europe'
    linkedDomains: [customDomain.id]
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
// DNS records added and Domain/SPF/DKIM/DKIM2 verified 2026-08-09.
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

output senderAddress string = 'donotreply@notify.smilrhq.dk'
