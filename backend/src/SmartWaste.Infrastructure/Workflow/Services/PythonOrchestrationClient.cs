using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Options;
using SmartWaste.Application.Workflow.DTOs.Transport;
using SmartWaste.Application.Workflow.Interfaces;

namespace SmartWaste.Infrastructure.Workflow.Services;

public sealed class PythonOrchestrationClient : IPythonOrchestrationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;
    private readonly string _serviceKey;
    private readonly ILogger<PythonOrchestrationClient> _logger;

    public PythonOrchestrationClient(HttpClient client, IConfiguration configuration, ILogger<PythonOrchestrationClient> logger)
    {
        _client = client;
        _serviceKey = configuration["InternalService:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public Task<PythonOrchestrationEnvelope> StartAsync(PythonWorkflowStartRequest request, CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/internal/agent-workflows/start", request, cancellationToken);

    public Task<PythonOrchestrationEnvelope> ResumeAfterCollectionApprovalAsync(PythonWorkflowResumeRequest request, CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/internal/agent-workflows/resume-after-collection-approval", request, cancellationToken);

    public Task<PythonOrchestrationEnvelope> ResumeAfterReportVerificationAsync(PythonWorkflowResumeRequest request, CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/internal/agent-workflows/resume-after-report-verification", request, cancellationToken);

    private async Task<PythonOrchestrationEnvelope> PostAsync<T>(string path, T request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_serviceKey))
            throw new AiServiceUnavailableException("Internal AI service credentials are not configured.");
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Internal-Service-Key", _serviceKey);
        try
        {
            using var response = await _client.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new BusinessRuleConflictException("The AI workflow snapshot cannot be resumed in its current state.");
            if (!response.IsSuccessStatusCode)
            {
                var diagnosticMessage = $"Internal AI service returned HTTP {(int)response.StatusCode}.";
                if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    var validationDetail = await ReadSafeValidationDetailAsync(response.Content, cancellationToken);
                    if (validationDetail is not null)
                        diagnosticMessage = $"Internal AI service returned HTTP 422: {validationDetail}";
                }
                throw new AiServiceUnavailableException(diagnosticMessage);
            }
            var result = await response.Content.ReadFromJsonAsync<PythonOrchestrationEnvelope>(JsonOptions, cancellationToken);
            if (result is null || result.WorkflowId is null ||
                result.Status is not ("Running" or "Paused" or "Completed" or "Failed") ||
                (result.TriggerType == SmartWaste.Domain.Workflow.Enums.AgentWorkflowTriggerType.ManualOperationalPlanning && result.TriggeringWasteReportId is not null) ||
                (result.TriggerType == SmartWaste.Domain.Workflow.Enums.AgentWorkflowTriggerType.CitizenReportSubmission && result.TriggeringWasteReportId is null) ||
                result.CurrentPhase is not ("NotStarted" or "Planning" or "RunningWasteAnalysis" or
                    "RunningCollectionPlanning" or "PausedForReportVerification" or
                    "PausedForCollectionApproval" or "RunningFleetPlanning" or
                    "RunningOperationalValidation" or "PausedForDispatchApproval" or
                    "DispatchNeedsRevision" or "Completed" or "Failed"))
                throw new AiServiceUnavailableException("Internal AI service returned an invalid orchestration response.");
            return result;
        }
        catch (HttpRequestException ex) { _logger.LogWarning(ex, "Internal AI orchestration request failed."); throw new AiServiceUnavailableException("Unable to reach the internal AI service.", ex); }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested) { throw new AiServiceUnavailableException("Internal AI service request timed out.", ex); }
        catch (JsonException ex) { throw new AiServiceUnavailableException("Internal AI service returned invalid JSON.", ex); }
    }

    private static async Task<string?> ReadSafeValidationDetailAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
            return null;

        try
        {
            var body = await content.ReadAsStringAsync(cancellationToken);
            if (body.Length > 8192)
                return null;
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("detail", out var detail) ||
                detail.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var error in detail.EnumerateArray())
            {
                if (error.ValueKind != JsonValueKind.Object ||
                    !error.TryGetProperty("loc", out var location) || location.ValueKind != JsonValueKind.Array ||
                    !error.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                    continue;

                // Use only known Pydantic error kinds. Raw messages and input values can
                // contain submitted data, so neither is copied into diagnostics.
                var safeMessage = type.GetString() switch
                {
                    "missing" => "Field required",
                    "list_type" => "Input should be a valid list",
                    "dict_type" or "model_type" => "Input should be a valid object",
                    "string_type" => "Input should be a valid string",
                    "uuid_parsing" or "uuid_type" => "Input should be a valid UUID",
                    "enum" or "literal_error" => "Invalid enum value",
                    _ => null
                };
                if (safeMessage is null)
                    continue;

                var path = new System.Text.StringBuilder();
                foreach (var segment in location.EnumerateArray())
                {
                    if (segment.ValueKind == JsonValueKind.Number && segment.TryGetInt32(out var index) && index >= 0)
                    {
                        path.Append('[').Append(index).Append(']');
                    }
                    else if (segment.ValueKind == JsonValueKind.String)
                    {
                        var name = segment.GetString();
                        if (string.IsNullOrEmpty(name) || name.Length > 64 ||
                            !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ||
                            new[] { "secret", "token", "password", "key", "authorization" }
                                .Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                            return null;
                        if (path.Length > 0)
                            path.Append('.');
                        path.Append(name);
                    }
                    else
                    {
                        return null;
                    }

                    if (path.Length > 160)
                        return null;
                }

                if (path.Length > 0 && path.ToString().StartsWith("body.", StringComparison.Ordinal))
                    return $"{path}: {safeMessage}";
            }
        }
        catch (JsonException)
        {
            // Preserve the bounded generic error for malformed FastAPI responses.
        }
        return null;
    }
}
