using System.Diagnostics;
using System.Net.Http.Json;
using DatabaseCloner.FunctionApp.Configuration;
using DatabaseCloner.FunctionApp.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DatabaseCloner.FunctionApp.Services;

public interface IWebIngestionService
{
    Task IngestAsync(CancellationToken cancellationToken = default);
}

public class WebIngestionService : IWebIngestionService
{
    private static readonly ActivitySource ActivitySource = new("DatabaseCloner.FunctionApp");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISqlWriter _sqlWriter;
    private readonly IngestionOptions _options;
    private readonly ILogger<WebIngestionService> _logger;

    public WebIngestionService(
        IHttpClientFactory httpClientFactory,
        ISqlWriter sqlWriter,
        IOptions<IngestionOptions> options,
        ILogger<WebIngestionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _sqlWriter = sqlWriter;
        _options = options.Value;
        _logger = logger;
    }

    public async Task IngestAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("WebIngestionService.Ingest");
        var client = _httpClientFactory.CreateClient("web-ingestion");
        var aggregated = new List<ResourceDto>();

        foreach (var endpoint in _options.Endpoints)
        {
            _logger.LogInformation("Requesting data from {Endpoint}", endpoint);
            var response = await client.GetAsync(endpoint, cancellationToken);
            activity?.AddEvent(new ActivityEvent("http.response", tags: new ActivityTagsCollection
            {
                { "endpoint", endpoint },
                { "status", (int)response.StatusCode }
            }));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to retrieve data from {Endpoint}. Status code: {Status}", endpoint, response.StatusCode);
                continue;
            }

            var resources = await response.Content.ReadFromJsonAsync<IEnumerable<ResourceDto>>(cancellationToken: cancellationToken) ?? Enumerable.Empty<ResourceDto>();
            aggregated.AddRange(resources);
        }

        if (aggregated.Count == 0)
        {
            _logger.LogInformation("No data received from ingestion endpoints");
            return;
        }

        activity?.SetTag("records.count", aggregated.Count);

        await _sqlWriter.BulkInsertAsync(aggregated, r => new[]
        {
            new SqlParameter { Value = r.Id },
            new SqlParameter { Value = r.Name },
            new SqlParameter { Value = r.Category },
            new SqlParameter { Value = r.LastUpdatedUtc }
        }, "dbo.Resources", cancellationToken);
    }
}
