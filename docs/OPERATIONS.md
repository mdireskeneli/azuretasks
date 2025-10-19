# Operations Guide

## Overview
This document describes the operational procedures for the Database Cloner Azure Function solution. The application runs two timer-triggered jobs:

1. **Database clone job** – clones the production database into an isolated environment for analytics/testing.
2. **Web ingestion job** – retrieves data from external APIs and persists it in Azure SQL with retry and resilience.

## Configuration Sources
Configuration is loaded in the following order:

1. `appsettings.json` and optional environment-specific files.
2. Environment variables.
3. Azure Key Vault secrets when `KeyVaultName` is provided.

Secrets such as SQL connection strings are stored in Key Vault and accessed via the function app's managed identity.

## Azure Resources
Infrastructure is provisioned via `infra/main.bicep` and includes:

- **App Service plan** (Consumption) hosting the Azure Function.
- **Storage Account** for Function runtime and triggers.
- **Application Insights** for telemetry.
- **Azure Key Vault** for secret management with RBAC access granted to the function's managed identity.
- **Azure SQL Server and Database** as the primary data store.

## Deployment Pipeline
CI/CD is orchestrated by the GitHub Actions workflow `deploy.yml`:

1. Restores, builds, and publishes the .NET isolated function app.
2. Deploys infrastructure using the Bicep template.
3. Deploys the function package to Azure.

Set the required secrets in the repository or organization settings prior to execution.

## Database Clone Job
- Triggered on the cron schedule defined by `JobSchedules:DatabaseCloneCron`.
- Uses the Azure Resource Manager SQL SDK to submit a copy operation of the source database.
- Waits for completion and logs status via Application Insights.
- Tagging and SKU of the cloned database is configurable via `DatabaseClone` options.

## Web Ingestion Job
- Triggered on `JobSchedules:IngestionCron`.
- Calls external APIs defined in `Ingestion:Endpoints` using resilient HTTP policies (retry with jitter and 429 handling).
- Persists results to `dbo.Resources` in Azure SQL using the `SqlWriter` bulk insert helper.

## Observability
- Application Insights automatically captures traces, metrics, and dependencies.
- Log queries can be executed in the Azure Portal via KQL against the Application Insights workspace.
- Timer trigger executions appear in the `traces` and `requests` tables with the function name as the operation.

## Maintenance Procedures
- **Rotating secrets:** update Key Vault secrets; no application restart is needed because the configuration provider refreshes on next access.
- **Pausing jobs:** update the timer cron expression in `JobSchedules` via App Settings or Key Vault secret references.
- **Database cleanup:** remove old cloned databases via Azure Portal or automation. Implement retention policies using Azure Automation or additional Functions if required.

## Troubleshooting
- Check Application Insights logs for exceptions or failed HTTP retries.
- Validate managed identity permissions if Key Vault or SQL access fails.
- Ensure SQL firewall rules allow the Function's outbound IPs or use VNet integration.

## Disaster Recovery
- Infrastructure can be redeployed using the Bicep template in a new region by adjusting the `location` parameter.
- SQL backups are managed by Azure SQL automatically; configure long-term retention per compliance needs.

