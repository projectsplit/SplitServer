namespace SplitServer.Configuration;

public class MongoDbSettings : ISettings
{
    public string ConnectionString { get; set; } = string.Empty;
    
    public string DatabaseName { get; set; } = string.Empty;
    
    public string SectionName { get; init; } = "MongoDb";
}