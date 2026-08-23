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

// Custom domain setup is a required two-phase deploy, confirmed via a deploy failure (2026-08-23,
// RequireCustomHostnameInEnvironment): Azure won't create a managed certificate for a hostname until
// that hostname is already bound to a container app/route in the environment — but binding with
// bindingType 'SniEnabled' requires a certificateId that must already exist. So: phase 1 (this false)
// binds the hostname with bindingType 'Disabled' and no managed cert at all, just to register it in the
// environment; phase 2 (redeploy with this true, after phase 1 succeeds) creates the managed cert — which
// validates ownership via the TXT record — and switches the binding to 'SniEnabled' referencing it.
param provisionManagedCertificate bool = false

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

// Managed certificate for the custom domain — only created in phase 2 (see provisionManagedCertificate
// above). By phase 2, the hostname is already bound to the container app live in Azure (from the phase 1
// deploy that ran first), which is what Microsoft.App/managedCertificates actually checks — so this
// resource doesn't need to depend on anything in *this* deployment for that. Must be HTTP validation, not
// TXT or CNAME — confirmed via two deploy failures (2026-08-23): CNAME isn't accepted for apex/root
// domains at all (Azure returned "Supported validation method(s) for the domain are: HTTP,TXT"), and TXT
// got accepted but then hung indefinitely at provisioningState 'Pending' — Microsoft's own docs
// (custom-domains-managed-certificates) pair apex/A-record domains with HTTP validation specifically, not
// TXT, and HTTP needs no extra DNS record (unlike TXT, which turned out to require a second, undocumented
// TXT record beyond the asuid ownership one, containing a validationToken Azure only exposes after the
// fact on the stuck resource). HTTP just needs the domain to already be reachable over plain HTTP, which
// it is (verified via curl against the phase 1 deploy).
resource managedCert 'Microsoft.App/managedEnvironments/managedCertificates@2024-03-01' = if (!empty(customDomainName) && provisionManagedCertificate) {
  parent: containerAppsEnv
  name: 'cert-${replace(customDomainName, '.', '-')}'
  location: location
  properties: {
    subjectName: customDomainName
    domainControlValidation: 'HTTP'
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${name}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  // In phase 2, the customDomains binding below (SniEnabled) references managedCert's certificateId via
  // resourceId() rather than a symbolic reference (see comment there), so ARM won't infer the ordering on
  // its own — without this explicit dependency, this update can race the cert's creation and fail with
  // CertificateNotFound (confirmed via a deploy failure, 2026-08-23). Safe to depend on even in phase
  // 1/no-domain, where managedCert isn't deployed at all — Bicep just skips the wait.
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
        // Phase 1 (provisionManagedCertificate = false): bind the hostname with no cert, just to
        // register it in the environment. Phase 2 (true): switch to SniEnabled referencing the now
        // (about to be, on this same deploy) created managed cert. See provisionManagedCertificate above.
        customDomains: !empty(customDomainName) ? [
          provisionManagedCertificate ? {
            name: customDomainName
            bindingType: 'SniEnabled'
            // Built via resourceId() rather than a symbolic reference to managedCert (above), since that
            // resource is itself conditional on the same customDomainName — referencing it symbolically
            // here would make Bicep try to resolve it even in the empty-string/no-op case.
            certificateId: resourceId('Microsoft.App/managedEnvironments/managedCertificates', containerAppsEnv.name, 'cert-${replace(customDomainName, '.', '-')}')
          } : {
            name: customDomainName
            bindingType: 'Disabled'
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
