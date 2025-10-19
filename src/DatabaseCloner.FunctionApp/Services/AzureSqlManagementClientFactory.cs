using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace DatabaseCloner.FunctionApp.Services;

public interface IAzureSqlManagementClientFactory
{
    Task<ArmClient> CreateAsync(CancellationToken cancellationToken = default);
}

public class AzureSqlManagementClientFactory : IAzureSqlManagementClientFactory
{
    private readonly ILogger<AzureSqlManagementClientFactory> _logger;

    public AzureSqlManagementClientFactory(ILogger<AzureSqlManagementClientFactory> logger)
    {
        _logger = logger;
    }

    public Task<ArmClient> CreateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating Azure ARM client using managed identity");
        var credential = new DefaultAzureCredential();
        var client = new ArmClient(credential);
        return Task.FromResult(client);
    }
}
