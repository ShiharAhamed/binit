using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace SmartWaste.Api.Authentication.InternalService;

/// <summary>
/// Dedicated ASP.NET Core authentication handler that validates internal service-to-service requests
/// using a configuration-backed shared secret key via the "X-Internal-Service-Key" header.
/// </summary>
public class InternalServiceAuthHandler : AuthenticationHandler<InternalServiceAuthOptions>
{
    private readonly IConfiguration _configuration;

    public InternalServiceAuthHandler(
        IOptionsMonitor<InternalServiceAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerName = Options.HeaderName ?? InternalServiceDefaults.HeaderName;
        if (!Request.Headers.TryGetValue(headerName, out var extractedValues) ||
            string.IsNullOrWhiteSpace(extractedValues.FirstOrDefault()))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing internal service key header."));
        }

        var extractedKey = extractedValues.First()!;
        var expectedKey = Options.ServiceKey;
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            expectedKey = _configuration["InternalService:ApiKey"];
        }

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            Logger.LogError("Internal service API key is not configured on the server.");
            return Task.FromResult(AuthenticateResult.Fail("Internal service API key is not configured."));
        }

        var extractedBytes = Encoding.UTF8.GetBytes(extractedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

        if (extractedBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(extractedBytes, expectedBytes))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal service key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "InternalAiService"),
            new Claim(ClaimTypes.Role, InternalServiceDefaults.Role)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Invalid or missing internal service credentials.",
            Instance = Request.Path
        };

        await Response.WriteAsJsonAsync(problemDetails);
    }
}
