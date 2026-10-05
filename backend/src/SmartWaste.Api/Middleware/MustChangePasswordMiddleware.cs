using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SmartWaste.Api.Middleware;

/// <summary>
/// Enforces mandatory password changes for authenticated users with MustChangePassword = true.
/// Restricts access strictly to password change and basic profile endpoints, blocking business APIs with 403 ProblemDetails.
/// </summary>
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth/change-password",
        "/api/v1/auth/me",
        "/health",
        "/api/v1/auth/logout"
    };

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustChangeClaim = context.User.FindFirst("must_change_password")?.Value;
            if (string.Equals(mustChangeClaim, "True", StringComparison.OrdinalIgnoreCase))
            {
                var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;

                // If path is not allowed for users pending mandatory password change
                if (!AllowedPaths.Contains(path) && !path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";

                    var problemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Password Change Required",
                        Detail = "Password change required before accessing this resource.",
                        Instance = context.Request.Path
                    };

                    var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await context.Response.WriteAsync(json);
                    return;
                }
            }
        }

        await _next(context);
    }
}
