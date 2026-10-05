using FluentAssertions;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.Validation;
using Xunit;

namespace SmartWaste.Tests.Workflow.Validation;

public class ApprovalRequestValidatorTests
{
    private readonly ApproveCollectionPlanningRequestValidator _approveCollectionValidator = new();
    private readonly RequestCollectionRevisionRequestValidator _revisionCollectionValidator = new();
    private readonly RejectCollectionPlanningRequestValidator _rejectCollectionValidator = new();
    private readonly ApproveDispatchPlanRequestValidator _approveDispatchValidator = new();
    private readonly RequestDispatchRevisionRequestValidator _revisionDispatchValidator = new();
    private readonly RejectDispatchPlanRequestValidator _rejectDispatchValidator = new();

    [Theory]
    [InlineData(1, "Looks good", true)]
    [InlineData(10, null, true)]
    [InlineData(0, "Valid", false)]
    [InlineData(-1, "Valid", false)]
    public void ApproveCollectionPlanningRequestValidator_ValidatesVersionAndReason(int version, string? reason, bool expectedValid)
    {
        var request = new ApproveCollectionPlanningRequest { ExpectedVersion = version, Reason = reason };
        var result = _approveCollectionValidator.Validate(request);
        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void ApproveCollectionPlanningRequestValidator_RejectsExcessivelyLongReason()
    {
        var request = new ApproveCollectionPlanningRequest { ExpectedVersion = 1, Reason = new string('A', 501) };
        var result = _approveCollectionValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Reason));
    }

    [Theory]
    [InlineData(1, "Please change window to afternoon.", true)]
    [InlineData(1, "", false)]
    [InlineData(1, "   ", false)]
    [InlineData(1, "Four", false)] // < 5 chars
    [InlineData(0, "Valid reason here", false)]
    public void RequestCollectionRevisionRequestValidator_ValidatesRequiredReason(int version, string reason, bool expectedValid)
    {
        var request = new RequestCollectionRevisionRequest { ExpectedVersion = version, Reason = reason };
        var result = _revisionCollectionValidator.Validate(request);
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(1, "Operational limits exceeded.", true)]
    [InlineData(1, "", false)]
    [InlineData(1, "   ", false)]
    [InlineData(1, "No", false)] // < 5 chars
    [InlineData(0, "Valid reason here", false)]
    public void RejectCollectionPlanningRequestValidator_ValidatesRequiredReason(int version, string reason, bool expectedValid)
    {
        var request = new RejectCollectionPlanningRequest { ExpectedVersion = version, Reason = reason };
        var result = _rejectCollectionValidator.Validate(request);
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(1, "Approved", true, true)]
    [InlineData(5, null, false, true)]
    [InlineData(0, "Approved", true, false)]
    public void ApproveDispatchPlanRequestValidator_ValidatesVersion(int version, string? reason, bool ack, bool expectedValid)
    {
        var request = new ApproveDispatchPlanRequest { ExpectedVersion = version, Reason = reason, AcknowledgeWarnings = ack };
        var result = _approveDispatchValidator.Validate(request);
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(1, "Driver schedule conflicts, replan.", true)]
    [InlineData(1, "", false)]
    [InlineData(1, "Tiny", false)]
    public void RequestDispatchRevisionRequestValidator_ValidatesRequiredReason(int version, string reason, bool expectedValid)
    {
        var request = new RequestDispatchRevisionRequest { ExpectedVersion = version, Reason = reason };
        var result = _revisionDispatchValidator.Validate(request);
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(1, "Weather emergency, cancel all routes.", true)]
    [InlineData(1, "", false)]
    [InlineData(1, "Bad", false)]
    public void RejectDispatchPlanRequestValidator_ValidatesRequiredReason(int version, string reason, bool expectedValid)
    {
        var request = new RejectDispatchPlanRequest { ExpectedVersion = version, Reason = reason };
        var result = _rejectDispatchValidator.Validate(request);
        result.IsValid.Should().Be(expectedValid);
    }
}
