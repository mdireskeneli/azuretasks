# Database Cloner Azure Functions

This repository contains an Azure Functions isolated worker that orchestrates two scheduled jobs:

1. **Database cloning** – copies a source Azure SQL database into a disposable environment for analytics or integration testing.
2. **Web service ingestion** – polls external APIs and persists responses into Azure SQL using resilient retry policies.

The project includes infrastructure-as-code, secure secret management via Key Vault, and a GitHub Actions pipeline for automated deployment.

## Solution Structure

```
.
├── src/DatabaseCloner.FunctionApp/   # Azure Functions project with DI, configuration, and timer triggers
├── infra/                            # Bicep template provisioning Azure resources
├── .github/workflows/                # CI/CD pipeline definition
└── docs/OPERATIONS.md                # Operational runbook
```

## Getting Started

1. Ensure the [.NET 8 SDK](https://dotnet.microsoft.com/download) and [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) are installed.
2. Restore dependencies and build:
   ```bash
   dotnet restore src/DatabaseCloner.FunctionApp/DatabaseCloner.FunctionApp.csproj
   dotnet build src/DatabaseCloner.FunctionApp/DatabaseCloner.FunctionApp.csproj
   ```
3. Configure local settings by copying `appsettings.json` to `local.settings.json` and providing values for SQL, Key Vault, and Application Insights connection strings.
4. Run the function host locally:
   ```bash
   func start --csharp
   ```

## Configuration

Configuration is bound to strongly-typed options classes and sourced from `appsettings.json`, environment variables, and Azure Key Vault (via managed identity).

| Setting | Description |
| ------- | ----------- |
| `Sql:PrimaryConnectionString` | Connection string used for web ingestion writes. Prefer Key Vault secret references in production. |
| `Sql:ResourceGroupName` | Resource group that contains the Azure SQL server/database. |
| `Sql:SourceDatabaseName` | Name of the source database that will be cloned. |
| `Sql:TargetDatabasePrefix` | Prefix applied to each clone, suffixed with a timestamp. |
| `Sql:ServerName` | Azure SQL server name (without the `.database.windows.net` suffix). |
| `Ingestion:BaseUrl` | Base URL for the web service to ingest. |
| `Ingestion:Endpoints` | Relative paths that will be queried. |
| `JobSchedules:DatabaseCloneCron` | Cron expression for the clone job (NCRONTAB format). |
| `JobSchedules:IngestionCron` | Cron expression for the ingestion job. |

## Infrastructure Deployment

Deploy Azure resources using the provided Bicep template:

```bash
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/main.bicep \
  --parameters \
      appServicePlanName=<plan> \
      functionAppName=<function-app> \
      storageAccountName=<storage> \
      appInsightsName=<app-insights> \
      keyVaultName=<key-vault> \
      sqlServerName=<sql-server> \
      sqlDatabaseName=<database> \
      sqlAdminLogin=<sql-admin> \
      sqlAdminPassword=<password>
```

## Deployment Pipeline

The GitHub Actions workflow builds, tests, deploys infrastructure, and publishes the function app. Configure the following secrets in the repository:

- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- `AZURE_RESOURCE_GROUP`
- `APP_SERVICE_PLAN_NAME`, `FUNCTION_APP_NAME`, `STORAGE_ACCOUNT_NAME`
- `APP_INSIGHTS_NAME`, `KEY_VAULT_NAME`
- `SQL_SERVER_NAME`, `SQL_DATABASE_NAME`, `SQL_ADMIN_LOGIN`, `SQL_ADMIN_PASSWORD`

## Observability

Application Insights provides logs, metrics, and traces for both functions. Use the Azure Portal or `az monitor app-insights` CLI commands to analyze telemetry.

## Operations

See [docs/OPERATIONS.md](docs/OPERATIONS.md) for operational procedures, troubleshooting, and maintenance guidance.

