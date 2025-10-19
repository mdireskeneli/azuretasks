namespace DatabaseCloner.FunctionApp.Configuration;

public class IngestionOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string[] Endpoints { get; set; } = Array.Empty<string>();
    public int RequestTimeoutSeconds { get; set; } = 100;
}
