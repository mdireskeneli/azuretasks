using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using DatabaseCloner.FunctionApp.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DatabaseCloner.FunctionApp.Services;

public interface IDatabaseCloneService
{
    Task<string> CloneAsync(CancellationToken cancellationToken = default);
}

public class DatabaseCloneService : IDatabaseCloneService
{
    private readonly IAzureSqlManagementClientFactory _clientFactory;
    private readonly SqlOptions _sqlOptions;
    private readonly DatabaseCloneOptions _cloneOptions;
    private readonly ILogger<DatabaseCloneService> _logger;

    public DatabaseCloneService(
        IAzureSqlManagementClientFactory clientFactory,
        IOptions<SqlOptions> sqlOptions,
        IOptions<DatabaseCloneOptions> cloneOptions,
        ILogger<DatabaseCloneService> logger)
    {
        _clientFactory = clientFactory;
        _sqlOptions = sqlOptions.Value;
        _cloneOptions = cloneOptions.Value;
        _logger = logger;
    }

    public async Task<string> CloneAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_sqlOptions.ServerName) || string.IsNullOrWhiteSpace(_sqlOptions.ResourceGroupName))
        {
            throw new InvalidOperationException("SQL server name or resource group is not configured");
        }

        var targetDatabaseName = $"{_sqlOptions.TargetDatabasePrefix}{DateTime.UtcNow:yyyyMMddHHmmss}";
        _logger.LogInformation("Starting database clone of {Source} to {Target}", _sqlOptions.SourceDatabaseName, targetDatabaseName);

        var armClient = await _clientFactory.CreateAsync(cancellationToken);
        var subscription = await armClient.GetDefaultSubscriptionAsync(cancellationToken);
        var resourceGroup = await subscription.GetResourceGroupAsync(_sqlOptions.ResourceGroupName, cancellationToken);
        var serverResource = await resourceGroup.Value.GetSqlServerAsync(_sqlOptions.ServerName, cancellationToken);
        var sourceDatabase = await serverResource.Value.GetSqlDatabaseAsync(_sqlOptions.SourceDatabaseName, cancellationToken);

        var parameters = new SqlDatabaseData(sourceDatabase.Value.Data.Location)
        {
            CreateMode = SqlDatabaseCreateMode.Copy,
            SourceDatabaseId = sourceDatabase.Value.Id,
            Sku = new SqlSku(_cloneOptions.TargetServiceObjectiveName)
            {
                Tier = _cloneOptions.TargetEdition
            }
        };

        foreach (var tag in _cloneOptions.Tags)
        {
            parameters.Tags[tag.Key] = tag.Value;
        }

        var operation = await serverResource.Value.GetSqlDatabases().CreateOrUpdateAsync(WaitUntil.Started, targetDatabaseName, parameters, cancellationToken);
        _logger.LogInformation("Clone request submitted for {Target}. Waiting for completion.", targetDatabaseName);

        await operation.WaitForCompletionAsync(cancellationToken);
        _logger.LogInformation("Database clone {Target} completed with status {Status}", targetDatabaseName, operation.GetRawResponse().Status);

        return targetDatabaseName;
    }
}
