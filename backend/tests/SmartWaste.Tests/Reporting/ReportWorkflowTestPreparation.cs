using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWaste.Application.Workflow.DTOs.Transport;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Reporting;

/// <summary>Prepares a linked report for human-decision HTTP tests without calling Python.</summary>
internal static class ReportWorkflowTestPreparation
{
    public static async Task MarkAwaitingVerificationAsync(IServiceProvider services, Guid reportId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = await db.AgentWorkflows.SingleAsync(w => w.TriggeringWasteReportId == reportId);
        if (workflow.Status != AgentWorkflowStatus.Created)
            throw new InvalidOperationException("Test workflow must still be Created.");
        workflow.Status = AgentWorkflowStatus.Planning;
        workflow.CurrentStep = WorkflowStepType.SharedPlanning;
        workflow.Version++;
        db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(), WorkflowId = workflow.Id,
            FromStatus = AgentWorkflowStatus.Created, ToStatus = AgentWorkflowStatus.Planning,
            Reason = "Test initial AI claim.", ChangedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var planner = JsonSerializer.SerializeToElement(new
        {
            objective = workflow.Objective,
            steps = new[]
            {
                new { stepId = "step-1", specialist = "WasteAnalysis", objective = "Analyze exact report.", dependsOn = Array.Empty<string>(), sequence = 1 },
                new { stepId = "step-2", specialist = "CollectionPlanning", objective = "Plan collection needs.", dependsOn = new[] { "step-1" }, sequence = 2 },
                new { stepId = "step-3", specialist = "FleetRoute", objective = "Plan routes.", dependsOn = new[] { "step-2" }, sequence = 3 },
                new { stepId = "step-4", specialist = "ValidationOperations", objective = "Validate plans.", dependsOn = new[] { "step-3" }, sequence = 4 }
            },
            summary = "Test specialist plan.", warnings = Array.Empty<string>(),
            agentName = "shared_planner_agent", modelName = "fake", advisoryOnly = true
        });
        var c1 = JsonSerializer.SerializeToElement(new
        {
            objective = "Analyze exact report.",
            analyses = new[] { new
            {
                reportId, categoryAssessment = "Reported roadside waste", recommendedPriority = "Medium",
                operationalConcerns = Array.Empty<string>(), recommendedHandling = "Staff review",
                confidence = "Medium", rationale = "Advisory analysis for test."
            } },
            sourcePage = 1, sourcePageSize = 1, sourceTotalCount = 1,
            agentName = "waste_analysis_agent", modelName = "fake", status = "completed"
        });
        await scope.ServiceProvider.GetRequiredService<IAgentWorkflowService>()
            .PersistReportVerificationPauseAsync(workflow.Id, new PythonOrchestrationEnvelope
            {
                WorkflowId = workflow.Id, Objective = workflow.Objective,
                TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
                TriggeringWasteReportId = reportId, Status = "Paused",
                CurrentPhase = "PausedForReportVerification", ApprovalStage = "ReportVerification",
                PauseReason = "Awaiting human report verification.",
                PlannerResult = planner, WasteAnalysisResult = c1,
                CompletedSpecialists = ["WasteAnalysis"],
                Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()),
                Warnings = JsonSerializer.SerializeToElement(Array.Empty<string>())
            }, workflow.Version);
    }
}
