namespace SmartWaste.Api.Authentication.InternalService;

/// <summary>
/// Constants for internal service-to-service authentication and authorization.
/// </summary>
public static class InternalServiceDefaults
{
    public const string AuthenticationScheme = "InternalService";
    public const string HeaderName = "X-Internal-Service-Key";
    public const string PolicyName = "InternalServicePolicy";
    public const string Role = "InternalService";
}
