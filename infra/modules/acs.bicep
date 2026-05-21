param name string
param location string

// ACS Communication + Email services are global resources; data residency is set via dataLocation.
resource communication 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-${name}'
  location: 'global'
  properties: {
    dataLocation: 'Europe'
    linkedDomains: [domain.id]
  }
}

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: 'email-${name}'
  location: 'global'
  properties: {
    dataLocation: 'Europe'
  }
}

// AzureManagedDomain gives an out-of-the-box sender domain (no custom DNS required).
// To use a custom domain (e.g. noreply@smilrhq.dk), replace this with a custom domain resource
// and complete DNS verification in the Azure portal before deploying.
resource domain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

@description('ACS connection string — store in Key Vault, never in plain config.')
@secure()
output connectionString string = communication.listKeys().primaryConnectionString

output senderAddress string = 'donotreply@${domain.properties.mailFromSenderDomain}'
