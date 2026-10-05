using System.Net;
using System.Text;
using System.Text.Json;
using SmartWaste.Domain.Workflow.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.DTOs.Transport;
using SmartWaste.Infrastructure.Workflow.Services;

namespace SmartWaste.Tests.Workflow.Services;

public sealed class PythonOrchestrationClientTests
{
    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } = null!;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Handler(request, cancellationToken);
    }

    private static (PythonOrchestrationClient Client, TestHttpMessageHandler Handler) CreateClient(string? serviceKey = "step-10b-test-key")
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8000/"),
            Timeout = TimeSpan.FromSeconds(300)
        };
        var values = new Dictionary<string, string?> { ["InternalService:ApiKey"] = serviceKey };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return (new PythonOrchestrationClient(httpClient, configuration, NullLogger<PythonOrchestrationClient>.Instance), handler);
    }

    [Fact]
    public async Task StartAsync_SendsAuthenticatedSinglePostAndDeserializesEnvelope()
    {
        var workflowId = Guid.NewGuid();
        var (client, handler) = CreateClient();
        var requestCount = 0;
        handler.Handler = async (request, _) =>
        {
            requestCount++;
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/internal/agent-workflows/start");
            request.Headers.GetValues("X-Internal-Service-Key").Should().ContainSingle().Which.Should().Be("step-10b-test-key");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            body.RootElement.GetProperty("workflowId").GetGuid().Should().Be(workflowId);
            body.RootElement.GetProperty("objective").GetString().Should().Be("Coordinate collection");
            body.RootElement.GetProperty("triggerType").GetString().Should().Be("ManualOperationalPlanning");
            body.RootElement.GetProperty("triggeringWasteReportId").ValueKind.Should().Be(JsonValueKind.Null);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"workflowId\":\"{workflowId}\",\"objective\":\"Coordinate collection\",\"status\":\"Paused\",\"currentPhase\":\"PausedForCollectionApproval\"}}", Encoding.UTF8, "application/json")
            };
        };

        var result = await client.StartAsync(new PythonWorkflowStartRequest { WorkflowId = workflowId, Objective = "Coordinate collection" });

        requestCount.Should().Be(1);
        result.WorkflowId.Should().Be(workflowId);
        result.CurrentPhase.Should().Be("PausedForCollectionApproval");
    }

    [Fact]
    public async Task CitizenStartAsync_SendsExactTriggerAndReportIdAndParsesReportPause()
    {
        var workflowId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var (client, handler) = CreateClient();
        handler.Handler = async (request, _) =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/internal/agent-workflows/start");
            request.Headers.GetValues("X-Internal-Service-Key").Should().ContainSingle();
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            body.RootElement.GetProperty("triggerType").GetString().Should().Be("CitizenReportSubmission");
            body.RootElement.GetProperty("triggeringWasteReportId").GetGuid().Should().Be(reportId);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"workflowId\":\"{workflowId}\",\"objective\":\"Analyze report\",\"triggerType\":\"CitizenReportSubmission\",\"triggeringWasteReportId\":\"{reportId}\",\"status\":\"Paused\",\"currentPhase\":\"PausedForReportVerification\",\"approvalStage\":\"ReportVerification\",\"errors\":[],\"warnings\":[]}}", Encoding.UTF8, "application/json")
            };
        };
        var result = await client.StartAsync(new PythonWorkflowStartRequest
        {
            WorkflowId = workflowId, Objective = "Analyze report",
            TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
            TriggeringWasteReportId = reportId
        });
        result.CurrentPhase.Should().Be("PausedForReportVerification");
        result.TriggerType.Should().Be(AgentWorkflowTriggerType.CitizenReportSubmission);
        result.TriggeringWasteReportId.Should().Be(reportId);
    }

    [Fact]
    public async Task ReportResumeAsync_SendsExactSnapshotListsAndVerifiedEvidence()
    {
        var workflowId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var (client, handler) = CreateClient();
        handler.Handler = async (request, _) =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/internal/agent-workflows/resume-after-report-verification");
            request.Headers.GetValues("X-Internal-Service-Key").Should().ContainSingle();
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var workflow = body.RootElement.GetProperty("workflow");
            workflow.GetProperty("triggerType").GetString().Should().Be("CitizenReportSubmission");
            workflow.GetProperty("triggeringWasteReportId").GetGuid().Should().Be(reportId);
            workflow.GetProperty("currentPhase").GetString().Should().Be("PausedForReportVerification");
            workflow.GetProperty("errors").GetArrayLength().Should().Be(0);
            workflow.GetProperty("warnings").GetArrayLength().Should().Be(0);
            var context = body.RootElement.GetProperty("resumeContext");
            context.GetProperty("approvalStage").GetString().Should().Be("ReportVerification");
            context.GetProperty("decision").GetString().Should().Be("Approved");
            context.GetProperty("authoritativeExecutionSummary").GetProperty("verifiedReportId").GetGuid().Should().Be(reportId);
            context.GetProperty("authoritativeExecutionSummary").GetProperty("reportStatus").GetString().Should().Be("Verified");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"workflowId\":\"{workflowId}\",\"objective\":\"Analyze report\",\"triggerType\":\"CitizenReportSubmission\",\"triggeringWasteReportId\":\"{reportId}\",\"status\":\"Paused\",\"currentPhase\":\"PausedForCollectionApproval\"}}", Encoding.UTF8, "application/json")
            };
        };
        using var summary = JsonDocument.Parse($"{{\"verifiedReportId\":\"{reportId}\",\"reportStatus\":\"Verified\"}}");
        var result = await client.ResumeAfterReportVerificationAsync(new PythonWorkflowResumeRequest
        {
            Workflow = new PythonOrchestrationEnvelope
            {
                WorkflowId = workflowId, Objective = "Analyze report",
                TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission, TriggeringWasteReportId = reportId,
                Status = "Paused", CurrentPhase = "PausedForReportVerification", ApprovalStage = "ReportVerification",
                Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()),
                Warnings = JsonSerializer.SerializeToElement(Array.Empty<string>())
            },
            ResumeContext = new PythonResumeContext
            {
                WorkflowId = workflowId, ApprovalStage = "ReportVerification", Decision = "Approved",
                AuthoritativeExecutionSummary = summary.RootElement.Clone()
            }
        });
        result.CurrentPhase.Should().Be("PausedForCollectionApproval");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(AiServiceUnavailableException))]
    [InlineData(HttpStatusCode.Conflict, typeof(BusinessRuleConflictException))]
    [InlineData(HttpStatusCode.UnprocessableEntity, typeof(AiServiceUnavailableException))]
    public async Task ReportResumeAsync_MapsFailuresWithoutRetry(HttpStatusCode code, Type expected)
    {
        var (client, handler) = CreateClient();
        var calls = 0;
        handler.Handler = (_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(code)); };
        var act = async () => await client.ResumeAfterReportVerificationAsync(new PythonWorkflowResumeRequest());
        await act.Should().ThrowAsync<Exception>().Where(ex => expected.IsInstanceOfType(ex));
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ReportResumeAsync_422UsesOnlySafeValidationLocation()
    {
        var (client, handler) = CreateClient(serviceKey: "private-test-key");
        handler.Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("{\"detail\":[{\"type\":\"missing\",\"loc\":[\"body\",\"workflow\",\"warnings\"],\"msg\":\"private-description\"}]}", Encoding.UTF8, "application/json")
        });
        var act = async () => await client.ResumeAfterReportVerificationAsync(new PythonWorkflowResumeRequest());
        var error = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        error.Which.Message.Should().Contain("body.workflow.warnings: Field required");
        error.Which.Message.Should().NotContain("private-description").And.NotContain("private-test-key");
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"workflowId\":\"00000000-0000-0000-0000-000000000001\",\"currentPhase\":\"NotARealPhase\"}")]
    [InlineData("{\"workflowId\":\"00000000-0000-0000-0000-000000000001\"}")]
    public async Task ReportResumeAsync_RejectsMalformedOrUnknownPhase(string body)
    {
        var (client, handler) = CreateClient();
        handler.Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var act = async () => await client.ResumeAfterReportVerificationAsync(new PythonWorkflowResumeRequest());
        await act.Should().ThrowAsync<AiServiceUnavailableException>();
    }

    [Fact]
    public async Task ResumeAsync_SerializesAuthoritativeSnapshotAndContext()
    {
        var workflowId = Guid.NewGuid();
        var (client, handler) = CreateClient();
        handler.Handler = async (request, _) =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/v1/internal/agent-workflows/resume-after-collection-approval");
            request.Headers.GetValues("X-Internal-Service-Key").Should().ContainSingle().Which.Should().Be("step-10b-test-key");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            body.RootElement.GetProperty("workflow").GetProperty("currentPhase").GetString().Should().Be("PausedForCollectionApproval");
            body.RootElement.GetProperty("resumeContext").GetProperty("authoritativeExecutionSummary").GetProperty("createdTaskCount").GetInt32().Should().Be(2);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"workflowId\":\"{workflowId}\",\"objective\":\"Coordinate collection\",\"status\":\"Paused\",\"currentPhase\":\"PausedForDispatchApproval\"}}", Encoding.UTF8, "application/json")
            };
        };

        using var summary = JsonDocument.Parse("{\"createdTaskCount\":2}");
        await client.ResumeAfterCollectionApprovalAsync(new PythonWorkflowResumeRequest
        {
            Workflow = new PythonOrchestrationEnvelope
            {
                WorkflowId = workflowId,
                Objective = "Coordinate collection",
                Status = "Paused",
                CurrentPhase = "PausedForCollectionApproval"
            },
            ResumeContext = new PythonResumeContext
            {
                WorkflowId = workflowId,
                AuthoritativeExecutionSummary = summary.RootElement.Clone()
            }
        });
    }

    [Fact]
    public async Task StartAsync_DoesNotRetryPost_WhenServiceReturnsFailure()
    {
        var (client, handler) = CreateClient();
        var requestCount = 0;
        handler.Handler = (_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        };

        var act = async () => await client.StartAsync(new PythonWorkflowStartRequest { WorkflowId = Guid.NewGuid(), Objective = "Coordinate collection" });

        await act.Should().ThrowAsync<AiServiceUnavailableException>();
        requestCount.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task StartAsync_MapsBoundedNonSuccessResponsesToAvailabilityFailure(HttpStatusCode statusCode)
    {
        var (client, handler) = CreateClient();
        handler.Handler = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode));

        var act = async () => await client.StartAsync(new PythonWorkflowStartRequest { WorkflowId = Guid.NewGuid(), Objective = "Coordinate collection" });

        var error = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        error.WithMessage($"*{(int)statusCode}*");
    }

    [Fact]
    public async Task ResumeAsync_MapsConflictToBusinessRuleConflict()
    {
        var (client, handler) = CreateClient();
        handler.Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));

        var act = async () => await client.ResumeAfterCollectionApprovalAsync(new PythonWorkflowResumeRequest());

        await act.Should().ThrowAsync<BusinessRuleConflictException>();
    }

    [Fact]
    public async Task ResumeAsync_422ReportsOnlySafeFastApiValidationLocationAndMessage()
    {
        var (client, handler) = CreateClient(serviceKey: "private-test-service-key");
        handler.Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("{\"detail\":[{\"type\":\"missing\",\"loc\":[\"body\",\"workflow\",\"errors\"],\"msg\":\"Field required\",\"input\":\"private-payload-value\"}]}", Encoding.UTF8, "application/json")
        });

        var act = async () => await client.ResumeAfterCollectionApprovalAsync(new PythonWorkflowResumeRequest());

        var error = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        error.Which.Message.Should().Contain("HTTP 422: body.workflow.errors: Field required");
        error.Which.Message.Should().NotContain("private-payload-value");
        error.Which.Message.Should().NotContain("private-test-service-key");
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"detail\":[{\"type\":\"value_error\",\"loc\":[\"body\",\"secretKey\"],\"msg\":\"private-token\"}]}")]
    public async Task ResumeAsync_422MalformedOrUnsafeBodyUsesGenericFallback(string body)
    {
        var (client, handler) = CreateClient();
        handler.Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

        var act = async () => await client.ResumeAfterCollectionApprovalAsync(new PythonWorkflowResumeRequest());

        var error = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        error.Which.Message.Should().Be("Internal AI service returned HTTP 422.");
        error.Which.Message.Should().NotContain("private-token");
    }

    [Fact]
    public async Task StartAsync_FailsClosed_WhenInternalServiceKeyIsMissing()
    {
        var (client, handler) = CreateClient(serviceKey: "");
        var called = false;
        handler.Handler = (_, _) =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };

        var act = async () => await client.StartAsync(new PythonWorkflowStartRequest { WorkflowId = Guid.NewGuid(), Objective = "Coordinate collection" });

        await act.Should().ThrowAsync<AiServiceUnavailableException>();
        called.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_MapsMalformedJsonToAvailabilityFailure()
    {
        var (client, handler) = CreateClient();
        handler.Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ invalid JSON", Encoding.UTF8, "application/json")
        });

        var act = async () => await client.StartAsync(new PythonWorkflowStartRequest { WorkflowId = Guid.NewGuid(), Objective = "Coordinate collection" });

        await act.Should().ThrowAsync<AiServiceUnavailableException>();
    }
}
