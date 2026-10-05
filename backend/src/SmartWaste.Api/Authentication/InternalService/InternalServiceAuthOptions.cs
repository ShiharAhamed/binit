using Microsoft.AspNetCore.Authentication;

namespace SmartWaste.Api.Authentication.InternalService;

/// <summary>
/// Configuration options for internal service-to-service authentication scheme.
/// </summary>
public class InternalServiceAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The HTTP header name containing the shared internal service key.
    /// Defaults to "X-Internal-Service-Key".
    /// </summary>
    public string HeaderName { get; set; } = InternalServiceDefaults.HeaderName;

    /// <summary>
    /// The expected shared secret key. Loaded securely from configuration/environment.
    /// </summary>
    public string? ServiceKey { get; set; }
}
