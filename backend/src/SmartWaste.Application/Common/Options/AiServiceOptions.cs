namespace SmartWaste.Application.Common.Options;

/// <summary>
/// Configuration options for communicating with the internal FastAPI AI service.
/// </summary>
public class AiServiceOptions
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";
    public int TimeoutSeconds { get; set; } = 300;
}
