using System.Diagnostics;
using DatabaseCloner.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DatabaseCloner.FunctionApp;

public class WebIngestionFunction
{
    private static readonly ActivitySource ActivitySource = new("DatabaseCloner.FunctionApp");

    private readonly IWebIngestionService _ingestionService;
    private readonly ILogger<WebIngestionFunction> _logger;

    public WebIngestionFunction(IWebIngestionService ingestionService, ILogger<WebIngestionFunction> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    [Function("WebIngestionJob")]
    public async Task RunAsync([TimerTrigger("%JobSchedules__IngestionCron%", RunOnStartup = false)] TimerInfo timerInfo)
    {
        using var activity = ActivitySource.StartActivity("WebIngestionJob");
        activity?.SetTag("schedule.next", timerInfo.ScheduleStatus?.Next?.UtcDateTime);

        _logger.LogInformation("Web ingestion job triggered at {Timestamp}. Next scheduled for {Next}", DateTimeOffset.UtcNow, timerInfo.ScheduleStatus?.Next);
        await _ingestionService.IngestAsync();
        _logger.LogInformation("Web ingestion job completed successfully");
    }
}
