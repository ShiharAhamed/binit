using FluentAssertions;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using Xunit;

namespace SmartWaste.Tests.Workflow;

public class AgentWorkflowTriggerFoundationTests
{
    [Fact]
    public void NewWorkflow_DefaultsToManualWithNoReportOrClaim()
    {
        var workflow = new AgentWorkflow();

        workflow.TriggerType.Should().Be(AgentWorkflowTriggerType.ManualOperationalPlanning);
        workflow.TriggeringWasteReportId.Should().BeNull();
        workflow.ProcessingLeaseId.Should().BeNull();
        workflow.ProcessingLeaseExpiresAt.Should().BeNull();
        workflow.ProcessingAttemptCount.Should().Be(0);
        workflow.Invoking(w => w.EnsureValidTrigger()).Should().NotThrow();
    }

    [Fact]
    public void CitizenTrigger_RequiresAuthoritativeReportId()
    {
        var workflow = new AgentWorkflow { TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission };
        workflow.Invoking(w => w.EnsureValidTrigger()).Should().Throw<InvalidOperationException>();

        workflow.TriggeringWasteReportId = Guid.NewGuid();
        workflow.Invoking(w => w.EnsureValidTrigger()).Should().NotThrow();
    }

    [Fact]
    public void ManualTrigger_RejectsReportId()
    {
        var workflow = new AgentWorkflow { TriggeringWasteReportId = Guid.NewGuid() };
        workflow.Invoking(w => w.EnsureValidTrigger()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReadDtos_DeriveReportReferenceOnlyForLinkedReport()
    {
        var reportId = Guid.Parse("e4b9a172-2211-4ccd-8855-112233445566");
        var manualSummary = new AgentWorkflowSummaryDto();
        var manualDetail = new AgentWorkflowDetailDto();
        manualSummary.TriggerType.Should().Be(AgentWorkflowTriggerType.ManualOperationalPlanning);
        manualSummary.TriggeringWasteReportId.Should().BeNull();
        manualSummary.ReportReference.Should().BeNull();
        manualDetail.ReportReference.Should().BeNull();

        var reportSummary = new AgentWorkflowSummaryDto
        {
            TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
            TriggeringWasteReportId = reportId
        };
        var reportDetail = new AgentWorkflowDetailDto
        {
            TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
            TriggeringWasteReportId = reportId
        };
        reportSummary.ReportReference.Should().Be("E4B9A172");
        reportDetail.ReportReference.Should().Be("E4B9A172");
        reportDetail.TriggeringWasteReportId.Should().Be(reportId);
    }
}
