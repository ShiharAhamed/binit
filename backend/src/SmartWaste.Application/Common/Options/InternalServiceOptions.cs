namespace SmartWaste.Application.Common.Options;

/// <summary>
/// Configuration options for service-to-service authentication with internal microservices.
/// </summary>
public class InternalServiceOptions
{
    public const string SectionName = "InternalService";

    /// <summary>
    /// Shared secret API key used for authenticating internal requests from the AI microservice.
    /// In production, this must be supplied via secure environment variables or secrets vault.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
