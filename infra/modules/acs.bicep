param name string
param location string

@description('Link the notify.smilrhq.dk custom domain (prod only — that domain\'s DNS is verified and linked to exactly one Communication Services resource at a time; a second environment trying to link it fails with DomainValidationError). Non-prod environments fall back to the Azure-managed domain instead, which also gives them a sender address distinct from prod\'s.')
param useCustomDomain bool = false

// ACS Communication + Email services are global resources; data residency is set via dataLocation.
resource communication 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-${name}'
  location: 'global'
  properties: {
    dataLocation: 'Europe'
    linkedDomains: [useCustomDomain ? customDomain.id : azureManagedDomain.id]
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
// Only created when useCustomDomain is true (prod) — see param description above.
resource customDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = if (useCustomDomain) {
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

output senderAddress string = useCustomDomain
  ? 'donotreply@notify.smilrhq.dk'
  : 'DoNotReply@${azureManagedDomain.properties.mailFromSenderDomain}'
