param name string
param location string

@description('Daily ingestion cap for the Log Analytics workspace in GB, as a string so fractional values work (e.g. \'0.1\'). \'-1\' means no cap. Once the cap is hit, ingestion stops until the next UTC day — so a cap trades losing telemetry during an incident for a bounded bill.')
param dailyQuotaGb string = '-1'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${name}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json(dailyQuotaGb)
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${name}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

output appInsightsConnectionString string = appInsights.properties.ConnectionString
output logAnalyticsWorkspaceId string = logAnalytics.id
