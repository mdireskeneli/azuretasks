namespace DatabaseCloner.FunctionApp.Configuration;

public class JobScheduleOptions
{
    public string DatabaseCloneCron { get; set; } = "0 0 2 * * *";
    public string IngestionCron { get; set; } = "0 0 */4 * * *";
}
