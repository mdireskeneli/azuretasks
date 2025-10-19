namespace DatabaseCloner.FunctionApp.Configuration;

public class DatabaseCloneOptions
{
    public string TargetEdition { get; set; } = "Standard";
    public string TargetServiceObjectiveName { get; set; } = "S1";
    public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
}
