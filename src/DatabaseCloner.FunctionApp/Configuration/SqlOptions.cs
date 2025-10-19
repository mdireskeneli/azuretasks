namespace DatabaseCloner.FunctionApp.Configuration;

public class SqlOptions
{
    public string PrimaryConnectionString { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string SourceDatabaseName { get; set; } = string.Empty;
    public string TargetDatabasePrefix { get; set; } = "Clone_";
    public string ServerName { get; set; } = string.Empty;
}
