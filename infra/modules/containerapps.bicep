param name string
param location string
param logAnalyticsWorkspaceId string
param keyVaultUri string
param appInsightsConnectionString string
param environment string
param imageName string

// Custom domain for the Container App (e.g. 'smilrhq.dk'). Left empty by default — see
// infra/parameters/prod.bicepparam for why this isn't set yet. Both the customDomains binding below and
// the managed certificate are fully conditional on this being non-empty, so leaving it '' deploys no
// domain-related resources at all.
param customDomainName string = ''

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${name}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: reference(logAnalyticsWorkspaceId, '2023-09-01').customerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
  }
}

// Managed certificate for the custom domain, only created once customDomainName is set (see its param
// comment above). Azure validates domain ownership via a TXT record (asuid.<domain>) you add at the DNS
// provider — not part of this deployment; see infra/parameters/prod.bicepparam for the follow-up steps.
// Must be TXT (not CNAME): CNAME domain control validation isn't supported for apex/root domains, since
// the apex CNAME is already used to point the domain at the Container App itself — confirmed via a
// deploy failure (2026-08-23) where Azure returned "Supported validation method(s) for the domain are:
// HTTP,TXT" for the apex domain smilrhq.dk.
resource managedCert 'Microsoft.App/managedEnvironments/managedCertificates@2024-03-01' = if (!empty(customDomainName)) {
  parent: containerAppsEnv
  name: 'cert-${replace(customDomainName, '.', '-')}'
  location: location
  properties: {
    subjectName: customDomainName
    domainControlValidation: 'TXT'
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${name}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  // Explicit dependency on managedCert: the customDomains binding below references it via resourceId()
  // rather than a symbolic reference (see comment there), which means ARM won't infer the ordering on
  // its own — without this, a first-time cert creation can race the container app's binding to it and
  // fail with CertificateNotFound. Safe to depend on even when customDomainName is empty and managedCert
  // isn't deployed at all; Bicep just skips the wait in that case.
  dependsOn: [
    managedCert
  ]
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        customDomains: !empty(customDomainName) ? [
          {
            name: customDomainName
            bindingType: 'SniEnabled'
            // Built via resourceId() rather than a symbolic reference to managedCert (below), since that
            // resource is itself conditional on the same customDomainName — referencing it symbolically
            // here would make Bicep try to resolve it even in the empty-string/no-op case.
            certificateId: resourceId('Microsoft.App/managedEnvironments/managedCertificates', containerAppsEnv.name, 'cert-${replace(customDomainName, '.', '-')}')
          }
        ] : []
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: imageName
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: environment == 'prod' ? 'Production' : 'Development' }
            { name: 'AZURE_KEY_VAULT_URI', value: keyVaultUri }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output principalId string = containerApp.identity.principalId
output hostname string = containerApp.properties.configuration.ingress.fqdn
