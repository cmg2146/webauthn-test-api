@description('Location for all resources.')
param location string = resourceGroup().location

@description('The name of the App Service Plan')
param appServicePlanName string = 'webauthn-test'

@description('The name of the Container Registry to pull images from')
param acrRegistryName string = 'cmgdev'

@description('The name of the SQL logical server.')
param sqlServerName string = 'webauthn-test'

@description('The name of the SQL Database.')
param sqlDatabaseName string = 'WebAuthnTest'

@description('The administrator username of the SQL logical server.')
param sqlAdminLogin string = 'sa'

@description('The administrator password of the SQL logical server.')
@secure()
param sqlAdminPassword string

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    tier: 'Free'
    name: 'F1'
  }
  properties: {
    reserved: true
    zoneRedundant: false
  }
}

resource webAppService 'Microsoft.Web/sites@2022-03-01' = {
  name: 'webauthn-test'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      acrUseManagedIdentityCreds: true
      appSettings: [
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrRegistryName}.azurecr.io/webauthn-test/web'
        }]
      http20Enabled: true
      minTlsVersion: '1.2'
    }
  }
}

resource apiAppService 'Microsoft.Web/sites@2022-03-01' = {
  name: 'webauthn-test-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      acrUseManagedIdentityCreds: true
      appSettings: [
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrRegistryName}.azurecr.io/webauthn-test/api'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APP_URL'
          value:
        }
        {
          name: 'AZURE_KEY_VAULT_ID'
          value:
        }]
      connectionStrings: [
        {
          name: 'Default'
          connectionString: 'Server=tcp:${sqlServerName}.database.widnows.net,1433;Initial Catalog=${sqlDatabaseName};MultipleActiveResultSets=True;Authentication=Active Directory Managed Identity;TrustServerCertificate=False;Encrypt=True'
          type: 'SQLAzure'
        }
      ]
      http20Enabled: true
      minTlsVersion: '1.2'
    }
  }
}

// TODO: Give the app service apps access to pull images from the ACR

// TODO: Create key vault. Give access to the api app service identity

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    //TODO: Cannot use microsoft accounts, must allow SQL and Azure Ad accounts
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

// TODO: give api app managed identiy access to the database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    capacity:
    family:
    name: 
    tier: 
    size: 
  }
}
