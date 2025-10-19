@description('Location for all resources')
param location string = resourceGroup().location

@description('Name of the App Service plan for the Function App')
param appServicePlanName string

@description('Name of the Function App')
param functionAppName string

@description('Name of the storage account used by the Function App')
param storageAccountName string

@description('Name of the Application Insights resource')
param appInsightsName string

@description('Name of the Key Vault')
param keyVaultName string

@description('Name of the SQL Server')
param sqlServerName string

@description('Admin login for SQL Server (used only for initial bootstrap)')
@secure()
param sqlAdminLogin string

@description('Admin password for SQL Server (used only for initial bootstrap)')
@secure()
param sqlAdminPassword string

@description('Name of the primary SQL Database')
param sqlDatabaseName string

@description('Optional tags applied to all resources')
param tags object = {}

resource plan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  tags: tags
}

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  tags: tags
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    enableRbacAuthorization: true
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
  }
  tags: tags
}

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: union(tags, {
    Role: 'Primary'
  })
  properties: {
    readScale: 'Disabled'
  }
  sku: {
    name: 'S2'
    tier: 'Standard'
  }
}

resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storage.listKeys().keys[0].value
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'Sql__ServerName'
          value: sqlServer.name
        }
        {
          name: 'Sql__ResourceGroupName'
          value: resourceGroup().name
        }
        {
          name: 'Sql__PrimaryConnectionString'
          value: listKeys(resourceId('Microsoft.Sql/servers/databases', sqlServerName, sqlDatabaseName), '2022-05-01-preview').primaryConnectionString
        }
        {
          name: 'KeyVaultName'
          value: keyVaultName
        }
      ]
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
    }
  }
  dependsOn: [
    storage
    plan
    keyVault
  ]
}

resource kvAccessPolicy 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.identity.principalId, 'keyvault-secrets-user')
  scope: keyVault
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalType: 'ServicePrincipal'
  }
}

output functionAppPrincipalId string = functionApp.identity.principalId
output keyVaultUri string = keyVault.properties.vaultUri
output sqlServerFullyQualifiedName string = '${sqlServerName}.database.windows.net'
