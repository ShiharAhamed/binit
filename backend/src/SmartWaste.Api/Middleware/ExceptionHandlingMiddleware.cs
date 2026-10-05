using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Exceptions;

namespace SmartWaste.Api.Middleware;

/// <summary>
/// Centralized exception handling middleware that catches application and unexpected exceptions,
/// mapping them to consistent ProblemDetails responses without leaking sensitive internals or stack traces.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred while processing request {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var problemDetails = exception switch
        {
            DuplicateEmailException dupEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.Conflict,
                Title = "Email Conflict",
                Detail = dupEx.Message,
                Instance = context.Request.Path
            },
            InvalidCredentialsException credEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.Unauthorized,
                Title = "Authentication Failed",
                Detail = credEx.Message,
                Instance = context.Request.Path
            },
            AccountInactiveException inactEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.Forbidden,
                Title = "Account Inactive",
                Detail = inactEx.Message,
                Instance = context.Request.Path
            },
            NotFoundException notFoundEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.NotFound,
                Title = "Resource Not Found",
                Detail = notFoundEx.Message,
                Instance = context.Request.Path
            },
            IdentityOperationException idEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Identity Error",
                Detail = string.Join("; ", idEx.Errors),
                Instance = context.Request.Path
            },
            AiServiceUnavailableException aiEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.ServiceUnavailable,
                Title = "AI Service Unavailable",
                Detail = aiEx.Message,
                Instance = context.Request.Path
            },
            UnsupportedClientRoleException unsupEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.Forbidden,
                Title = "Unsupported Client Role",
                Detail = unsupEx.Message,
                Instance = context.Request.Path,
                Extensions = { ["errorCode"] = unsupEx.ErrorCode }
            },
            PasswordChangeRequiredException pwdReqEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.Forbidden,
                Title = "Password Change Required",
                Detail = pwdReqEx.Message,
                Instance = context.Request.Path
            },
            InvalidRoleException invRoleEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Role",
                Detail = invRoleEx.Message,
                Instance = context.Request.Path
            },
            UserManagementException userMgmtEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "User Management Error",
                Detail = userMgmtEx.Message,
                Instance = context.Request.Path
            },
            ForbiddenException forbEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.Forbidden,
                Title = "Forbidden",
                Detail = forbEx.Message,
                Instance = context.Request.Path
            },
            InvalidWorkflowTransitionException transEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.Conflict,
                Title = "Invalid Workflow State Transition",
                Detail = transEx.Message,
                Instance = context.Request.Path
            },
            BusinessRuleConflictException confEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.Conflict,
                Title = "Business Rule Conflict",
                Detail = confEx.Message,
                Instance = context.Request.Path
            },
            FileValidationException fileValEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid File",
                Detail = fileValEx.Message,
                Instance = context.Request.Path
            },
            StorageServiceException storEx => new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Storage Service Error",
                Detail = storEx.Message,
                Instance = context.Request.Path
            },
            FluentValidation.ValidationException valEx => CreateValidationProblemDetails(context, valEx),
            _ => new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while processing your request. Please try again later.",
                Instance = context.Request.Path
            }
        };

        context.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;

        var json = JsonSerializer.Serialize(problemDetails, problemDetails.GetType(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext context,
        FluentValidation.ValidationException valEx)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (valEx.Errors != null && valEx.Errors.Any())
        {
            errors = valEx.Errors
                .GroupBy(
                    e => ToCamelCase(string.IsNullOrWhiteSpace(e.PropertyName) ? string.Empty : e.PropertyName),
                    e => e.ErrorMessage)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToArray(),
                    StringComparer.OrdinalIgnoreCase);
        }
        else if (!string.IsNullOrWhiteSpace(valEx.Message))
        {
            errors[string.Empty] = new[] { valEx.Message };
        }

        var detail = valEx.Errors != null && valEx.Errors.Any()
            ? "One or more validation errors occurred."
            : (!string.IsNullOrWhiteSpace(valEx.Message) ? valEx.Message : "One or more validation errors occurred.");

        return new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Status = (int)HttpStatusCode.BadRequest,
            Title = "Validation Failed",
            Detail = detail,
            Instance = context.Request.Path
        };
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var parts = name.Split('.');
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length > 0)
            {
                parts[i] = char.ToLowerInvariant(part[0]) + part[1..];
            }
        }

        return string.Join('.', parts);
    }
}
