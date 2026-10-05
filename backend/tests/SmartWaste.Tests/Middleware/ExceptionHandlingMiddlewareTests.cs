using System.Net;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Api.Middleware;
using SmartWaste.Application.Common.Exceptions;
using Xunit;

namespace SmartWaste.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly NullLogger<ExceptionHandlingMiddleware> _logger = NullLogger<ExceptionHandlingMiddleware>.Instance;

    private static (DefaultHttpContext context, MemoryStream responseStream) CreateHttpContext(string path = "/api/v1/test")
    {
        var context = new DefaultHttpContext();
        var stream = new MemoryStream();
        context.Response.Body = stream;
        context.Request.Path = path;
        return (context, stream);
    }

    private static async Task<string> ReadResponseBodyAsync(MemoryStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_OneFluentValidationError_Returns400WithValidationProblemDetails()
    {
        var (context, stream) = CreateHttpContext("/api/v1/collection-tasks/reschedule");
        var failures = new[]
        {
            new ValidationFailure("Reason", "Reason must be between 5 and 500 characters.")
        };
        RequestDelegate next = _ => throw new ValidationException(failures);
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        context.Response.ContentType.Should().Be("application/problem+json");

        var body = await ReadResponseBodyAsync(stream);
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation Failed");
        problem.Detail.Should().Be("One or more validation errors occurred.");
        problem.Instance.Should().Be("/api/v1/collection-tasks/reschedule");
        problem.Errors.Should().ContainKey("reason");
        problem.Errors["reason"].Should().ContainSingle().Which.Should().Be("Reason must be between 5 and 500 characters.");
    }

    [Fact]
    public async Task InvokeAsync_MultiplePropertyValidationErrors_Returns400AndAggregatesProperly()
    {
        var (context, stream) = CreateHttpContext("/api/v1/collection-tasks");
        var failures = new[]
        {
            new ValidationFailure("NewScheduledAt", "New scheduled time is required."),
            new ValidationFailure("Reason", "Reason is required."),
            new ValidationFailure("Reason", "Reason must be between 5 and 500 characters.")
        };
        RequestDelegate next = _ => throw new ValidationException(failures);
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        var body = await ReadResponseBodyAsync(stream);
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation Failed");
        problem.Errors.Should().ContainKey("newScheduledAt");
        problem.Errors["newScheduledAt"].Should().ContainSingle().Which.Should().Be("New scheduled time is required.");
        problem.Errors.Should().ContainKey("reason");
        problem.Errors["reason"].Should().HaveCount(2);
        problem.Errors["reason"].Should().Contain("Reason is required.");
        problem.Errors["reason"].Should().Contain("Reason must be between 5 and 500 characters.");
    }

    [Fact]
    public async Task InvokeAsync_SingleMessageValidationException_Returns400WithDetailAndModelError()
    {
        var (context, stream) = CreateHttpContext("/api/v1/collection-tasks/reschedule");
        RequestDelegate next = _ => throw new ValidationException("New scheduled time cannot be in the past.");
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        var body = await ReadResponseBodyAsync(stream);
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Validation Failed");
        problem.Detail.Should().Be("New scheduled time cannot be in the past.");
        problem.Errors.Should().ContainKey(string.Empty);
        problem.Errors[string.Empty].Should().ContainSingle().Which.Should().Be("New scheduled time cannot be in the past.");
    }

    [Fact]
    public async Task InvokeAsync_BusinessRuleConflictException_Preserves409Conflict()
    {
        var (context, stream) = CreateHttpContext("/api/v1/collection-tasks");
        RequestDelegate next = _ => throw new BusinessRuleConflictException("An active collection task already exists for this target.");
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);

        var body = await ReadResponseBodyAsync(stream);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Business Rule Conflict");
        problem.Detail.Should().Be("An active collection task already exists for this target.");
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Preserves404NotFound()
    {
        var (context, stream) = CreateHttpContext("/api/v1/collection-tasks/123");
        RequestDelegate next = _ => throw new NotFoundException("Collection task '123' was not found.");
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        var body = await ReadResponseBodyAsync(stream);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource Not Found");
        problem.Detail.Should().Be("Collection task '123' was not found.");
    }

    [Fact]
    public async Task InvokeAsync_ForbiddenException_Preserves403Forbidden()
    {
        var (context, stream) = CreateHttpContext("/api/v1/collection-tasks");
        RequestDelegate next = _ => throw new ForbiddenException("Only Waste Officers can reschedule collection tasks.");
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);

        var body = await ReadResponseBodyAsync(stream);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden");
        problem.Detail.Should().Be("Only Waste Officers can reschedule collection tasks.");
    }

    [Fact]
    public async Task InvokeAsync_UnexpectedException_Returns500WithoutLeakingSecretsOrInternalDetails()
    {
        var (context, stream) = CreateHttpContext("/api/v1/collection-tasks");
        RequestDelegate next = _ => throw new InvalidOperationException("Fatal database error: Server=prod-db.internal;User Id=postgres;Password=SuperSecretPassword123!; StackTrace: at SecretModule.Execute()");
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);

        var body = await ReadResponseBodyAsync(stream);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Title.Should().Be("Internal Server Error");
        problem.Detail.Should().Be("An unexpected error occurred while processing your request. Please try again later.");

        body.Should().NotContain("SuperSecretPassword123!");
        body.Should().NotContain("prod-db.internal");
        body.Should().NotContain("SecretModule.Execute");
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_PassesThroughWithoutError()
    {
        var (context, _) = CreateHttpContext("/api/v1/healthy");
        var nextInvoked = false;
        RequestDelegate next = ctx =>
        {
            nextInvoked = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        var middleware = new ExceptionHandlingMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public void AppSettings_ContainsValidMunicipalityTimeZoneConfiguration()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SmartWaste.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("SmartWaste.sln root should be discoverable from test execution path.");
        var appsettingsPath = Path.Combine(dir!.FullName, "src", "SmartWaste.Api", "appsettings.json");
        File.Exists(appsettingsPath).Should().BeTrue("appsettings.json must exist in SmartWaste.Api.");

        var json = File.ReadAllText(appsettingsPath);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("Municipality", out var municipalityProp).Should().BeTrue("appsettings.json must contain 'Municipality' section.");
        municipalityProp.TryGetProperty("TimeZoneId", out var timeZoneProp).Should().BeTrue("'Municipality' section must contain 'TimeZoneId'.");
        var timeZoneId = timeZoneProp.GetString();
        timeZoneId.Should().Be("Asia/Colombo");

        // Verify cross-platform timezone resolution
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId!);
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
        }

        tz.Should().NotBeNull();
        tz.BaseUtcOffset.Should().Be(TimeSpan.FromHours(5.5));
    }
}
