using System.Diagnostics;
using DatabaseCloner.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DatabaseCloner.FunctionApp;

public class DatabaseCloneFunction
{
    private static readonly ActivitySource ActivitySource = new("DatabaseCloner.FunctionApp");

    private readonly IDatabaseCloneService _cloneService;
    private readonly ILogger<DatabaseCloneFunction> _logger;

    public DatabaseCloneFunction(
        IDatabaseCloneService cloneService,
        ILogger<DatabaseCloneFunction> logger)
    {
        _cloneService = cloneService;
        _logger = logger;
    }

    [Function("DatabaseCloneJob")]
    public async Task RunAsync([TimerTrigger("%JobSchedules__DatabaseCloneCron%", RunOnStartup = false)] TimerInfo timerInfo)
    {
        using var activity = ActivitySource.StartActivity("DatabaseCloneJob");
        activity?.SetTag("schedule.next", timerInfo.ScheduleStatus?.Next?.UtcDateTime);

        _logger.LogInformation("Database clone job triggered at {Timestamp}. Schedule status: {Status}", DateTimeOffset.UtcNow, timerInfo.ScheduleStatus?.Next);
        var databaseName = await _cloneService.CloneAsync();
        activity?.SetTag("database.clone.name", databaseName);
        _logger.LogInformation("Database clone job finished successfully. Clone created: {Database}", databaseName);
    }
}
