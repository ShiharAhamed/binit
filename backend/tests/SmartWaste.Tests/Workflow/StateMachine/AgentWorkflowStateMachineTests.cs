using FluentAssertions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Domain.Workflow.Enums;
using Xunit;

namespace SmartWaste.Tests.Workflow.StateMachine;

public class AgentWorkflowStateMachineTests
{
    private readonly AgentWorkflowStateMachine _sut = new();

    // =========================================================================
    // Legal Transitions (A, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T)
    // =========================================================================

    [Theory]
    [InlineData(AgentWorkflowStatus.Created, AgentWorkflowStatus.Planning)] // A
    [InlineData(AgentWorkflowStatus.Created, AgentWorkflowStatus.Rejected)] // Source report cancelled before AI starts.
    [InlineData(AgentWorkflowStatus.Planning, AgentWorkflowStatus.AwaitingCollectionApproval)] // C
    [InlineData(AgentWorkflowStatus.Planning, AgentWorkflowStatus.Failed)] // D
    [InlineData(AgentWorkflowStatus.Planning, AgentWorkflowStatus.Rejected)] // Source report cancelled during C1.
    [InlineData(AgentWorkflowStatus.Planning, AgentWorkflowStatus.AwaitingReportVerification)]
    [InlineData(AgentWorkflowStatus.AwaitingReportVerification, AgentWorkflowStatus.Planning)]
    [InlineData(AgentWorkflowStatus.AwaitingReportVerification, AgentWorkflowStatus.Rejected)]
    [InlineData(AgentWorkflowStatus.AwaitingCollectionApproval, AgentWorkflowStatus.CollectionApproved)] // E
    [InlineData(AgentWorkflowStatus.AwaitingCollectionApproval, AgentWorkflowStatus.CollectionNeedsRevision)] // F
    [InlineData(AgentWorkflowStatus.AwaitingCollectionApproval, AgentWorkflowStatus.Rejected)] // G
    [InlineData(AgentWorkflowStatus.CollectionNeedsRevision, AgentWorkflowStatus.Planning)] // H
    [InlineData(AgentWorkflowStatus.CollectionNeedsRevision, AgentWorkflowStatus.Rejected)]
    [InlineData(AgentWorkflowStatus.CollectionApproved, AgentWorkflowStatus.CreatingScheduledTasks)] // I
    [InlineData(AgentWorkflowStatus.CollectionApproved, AgentWorkflowStatus.Failed)]
    [InlineData(AgentWorkflowStatus.CreatingScheduledTasks, AgentWorkflowStatus.FleetPlanning)] // J
    [InlineData(AgentWorkflowStatus.CreatingScheduledTasks, AgentWorkflowStatus.Failed)]
    [InlineData(AgentWorkflowStatus.FleetPlanning, AgentWorkflowStatus.OperationalValidation)] // K
    [InlineData(AgentWorkflowStatus.FleetPlanning, AgentWorkflowStatus.Failed)]
    [InlineData(AgentWorkflowStatus.OperationalValidation, AgentWorkflowStatus.AwaitingDispatchApproval)] // L
    [InlineData(AgentWorkflowStatus.OperationalValidation, AgentWorkflowStatus.DispatchNeedsRevision)] // M
    [InlineData(AgentWorkflowStatus.OperationalValidation, AgentWorkflowStatus.Failed)]
    [InlineData(AgentWorkflowStatus.AwaitingDispatchApproval, AgentWorkflowStatus.DispatchApproved)] // N
    [InlineData(AgentWorkflowStatus.AwaitingDispatchApproval, AgentWorkflowStatus.DispatchNeedsRevision)] // O
    [InlineData(AgentWorkflowStatus.AwaitingDispatchApproval, AgentWorkflowStatus.Rejected)] // P
    [InlineData(AgentWorkflowStatus.DispatchNeedsRevision, AgentWorkflowStatus.FleetPlanning)] // Q
    [InlineData(AgentWorkflowStatus.DispatchNeedsRevision, AgentWorkflowStatus.Rejected)]
    [InlineData(AgentWorkflowStatus.DispatchApproved, AgentWorkflowStatus.ExecutingAssignments)] // R
    [InlineData(AgentWorkflowStatus.DispatchApproved, AgentWorkflowStatus.Failed)]
    [InlineData(AgentWorkflowStatus.ExecutingAssignments, AgentWorkflowStatus.Completed)] // S
    [InlineData(AgentWorkflowStatus.ExecutingAssignments, AgentWorkflowStatus.Failed)] // T
    public void CanTransition_ValidTransitions_ReturnsTrue_AndDoesNotThrow(AgentWorkflowStatus from, AgentWorkflowStatus to)
    {
        var canTransition = _sut.CanTransition(from, to);
        canTransition.Should().BeTrue();

        var act = () => _sut.EnsureCanTransition(from, to);
        act.Should().NotThrow();
    }

    // =========================================================================
    // Illegal Transitions (B, U, V, W, X, Y)
    // =========================================================================

    [Fact]
    public void CanTransition_CreatedToDispatchApproved_ReturnsFalse_AndThrows() // B
    {
        _sut.CanTransition(AgentWorkflowStatus.Created, AgentWorkflowStatus.DispatchApproved).Should().BeFalse();

        var act = () => _sut.EnsureCanTransition(AgentWorkflowStatus.Created, AgentWorkflowStatus.DispatchApproved);
        act.Should().Throw<InvalidWorkflowTransitionException>()
            .WithMessage("*Created*DispatchApproved*");
    }

    [Theory]
    [InlineData(AgentWorkflowStatus.Completed, AgentWorkflowStatus.Planning)] // U
    [InlineData(AgentWorkflowStatus.Completed, AgentWorkflowStatus.Created)]
    [InlineData(AgentWorkflowStatus.Completed, AgentWorkflowStatus.ExecutingAssignments)]
    [InlineData(AgentWorkflowStatus.Rejected, AgentWorkflowStatus.Planning)] // V
    [InlineData(AgentWorkflowStatus.Rejected, AgentWorkflowStatus.Created)]
    [InlineData(AgentWorkflowStatus.Rejected, AgentWorkflowStatus.AwaitingCollectionApproval)]
    [InlineData(AgentWorkflowStatus.Failed, AgentWorkflowStatus.Planning)] // W
    [InlineData(AgentWorkflowStatus.Failed, AgentWorkflowStatus.Created)]
    [InlineData(AgentWorkflowStatus.Failed, AgentWorkflowStatus.Completed)]
    public void CanTransition_FromTerminalStates_ReturnsFalse_AndThrows(AgentWorkflowStatus terminalStatus, AgentWorkflowStatus to)
    {
        _sut.IsTerminal(terminalStatus).Should().BeTrue();
        _sut.CanTransition(terminalStatus, to).Should().BeFalse();

        var act = () => _sut.EnsureCanTransition(terminalStatus, to);
        act.Should().Throw<InvalidWorkflowTransitionException>()
            .WithMessage("*terminal*");
    }

    [Theory]
    [InlineData(AgentWorkflowStatus.Created)]
    [InlineData(AgentWorkflowStatus.Planning)] // X
    [InlineData(AgentWorkflowStatus.AwaitingCollectionApproval)]
    [InlineData(AgentWorkflowStatus.CollectionApproved)]
    [InlineData(AgentWorkflowStatus.FleetPlanning)]
    [InlineData(AgentWorkflowStatus.AwaitingDispatchApproval)]
    [InlineData(AgentWorkflowStatus.Completed)]
    public void CanTransition_SameState_ReturnsFalse_AndThrows(AgentWorkflowStatus status)
    {
        _sut.CanTransition(status, status).Should().BeFalse();

        var act = () => _sut.EnsureCanTransition(status, status);
        act.Should().Throw<InvalidWorkflowTransitionException>()
            .WithMessage("*Same-state*");
    }

    [Theory]
    [InlineData(AgentWorkflowStatus.Planning, AgentWorkflowStatus.DispatchApproved)] // Y
    [InlineData(AgentWorkflowStatus.Created, AgentWorkflowStatus.Completed)]
    [InlineData(AgentWorkflowStatus.Created, AgentWorkflowStatus.FleetPlanning)]
    [InlineData(AgentWorkflowStatus.AwaitingCollectionApproval, AgentWorkflowStatus.FleetPlanning)]
    [InlineData(AgentWorkflowStatus.AwaitingReportVerification, AgentWorkflowStatus.CollectionApproved)]
    [InlineData(AgentWorkflowStatus.CreatingScheduledTasks, AgentWorkflowStatus.Completed)]
    public void CanTransition_ArbitrarySkipping_ReturnsFalse_AndThrows(AgentWorkflowStatus from, AgentWorkflowStatus to)
    {
        _sut.CanTransition(from, to).Should().BeFalse();

        var act = () => _sut.EnsureCanTransition(from, to);
        act.Should().Throw<InvalidWorkflowTransitionException>();
    }

    [Fact]
    public void GetPermittedTransitions_ReturnsCorrectSubset()
    {
        var transitions = _sut.GetPermittedTransitions(AgentWorkflowStatus.AwaitingCollectionApproval);
        transitions.Should().BeEquivalentTo(new[]
        {
            AgentWorkflowStatus.CollectionApproved,
            AgentWorkflowStatus.CollectionNeedsRevision,
            AgentWorkflowStatus.Rejected
        });

        var terminal = _sut.GetPermittedTransitions(AgentWorkflowStatus.Completed);
        terminal.Should().BeEmpty();
    }

    [Fact]
    public void LegalTransitions_HasExactlyThirtyOneLegalTransitions()
    {
        var allStatuses = Enum.GetValues<AgentWorkflowStatus>();
        var totalTransitions = allStatuses.Sum(s => _sut.GetPermittedTransitions(s).Count);
        totalTransitions.Should().Be(31);
    }

    [Fact]
    public void CanTransition_CollectionNeedsRevision_SupportsPlanningAndRejected()
    {
        _sut.CanTransition(AgentWorkflowStatus.CollectionNeedsRevision, AgentWorkflowStatus.Planning).Should().BeTrue();
        _sut.CanTransition(AgentWorkflowStatus.CollectionNeedsRevision, AgentWorkflowStatus.Rejected).Should().BeTrue();
        _sut.CanTransition(AgentWorkflowStatus.CollectionNeedsRevision, AgentWorkflowStatus.Completed).Should().BeFalse();

        _sut.EnsureCanTransition(AgentWorkflowStatus.CollectionNeedsRevision, AgentWorkflowStatus.Planning);
        _sut.EnsureCanTransition(AgentWorkflowStatus.CollectionNeedsRevision, AgentWorkflowStatus.Rejected);
    }

    [Fact]
    public void CanTransition_DispatchNeedsRevision_SupportsFleetPlanningAndRejected()
    {
        _sut.CanTransition(AgentWorkflowStatus.DispatchNeedsRevision, AgentWorkflowStatus.FleetPlanning).Should().BeTrue();
        _sut.CanTransition(AgentWorkflowStatus.DispatchNeedsRevision, AgentWorkflowStatus.Rejected).Should().BeTrue();
        _sut.CanTransition(AgentWorkflowStatus.DispatchNeedsRevision, AgentWorkflowStatus.Completed).Should().BeFalse();

        _sut.EnsureCanTransition(AgentWorkflowStatus.DispatchNeedsRevision, AgentWorkflowStatus.FleetPlanning);
        _sut.EnsureCanTransition(AgentWorkflowStatus.DispatchNeedsRevision, AgentWorkflowStatus.Rejected);
    }
}
