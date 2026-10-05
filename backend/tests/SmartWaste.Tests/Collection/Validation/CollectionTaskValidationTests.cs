using FluentAssertions;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Collection.Validation;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Reporting.Enums;
using Xunit;

namespace SmartWaste.Tests.Collection.Validation;

public class CollectionTaskValidationTests
{
    private readonly CreateManualCollectionTaskRequestValidator _createTaskValidator = new();
    private readonly RescheduleCollectionTaskRequestValidator _rescheduleValidator = new();
    private readonly CollectionNeedListQueryValidator _needQueryValidator = new();
    private readonly CollectionTaskListQueryValidator _taskListQueryValidator = new();

    #region CreateManualCollectionTaskRequest Tests

    [Fact]
    public void CreateManualCollectionTaskRequest_ValidReportTarget_ShouldPassValidation()
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = Guid.NewGuid(),
            WasteBinId = null,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            HandlingNotes = "Bulky compaction required."
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(CollectionReason.FullOrBlockedBin)]
    [InlineData(CollectionReason.RoutineCollection)]
    public void CreateManualCollectionTaskRequest_ValidBinTarget_ShouldPassValidation(CollectionReason reason)
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = null,
            WasteBinId = Guid.NewGuid(),
            CollectionReason = reason,
            ScheduledAt = DateTime.UtcNow.AddHours(3)
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateManualCollectionTaskRequest_ValidOfficerDiscretion_ShouldPassValidation()
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = null,
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.OfficerDiscretion,
            ScheduledAt = DateTime.UtcNow.AddHours(4),
            SchedulingReason = "Anticipated overflow due to weekend cultural festival."
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateManualCollectionTaskRequest_BothTargetsNull_ShouldFailValidation()
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = null,
            WasteBinId = null,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Exactly one of WasteReportId or WasteBinId must be supplied", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateManualCollectionTaskRequest_BothTargetsPopulated_ShouldFailValidation()
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = Guid.NewGuid(),
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Exactly one of WasteReportId or WasteBinId must be supplied", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(CollectionReason.FullOrBlockedBin)]
    [InlineData(CollectionReason.RoutineCollection)]
    [InlineData(CollectionReason.OfficerDiscretion)]
    public void CreateManualCollectionTaskRequest_ReportTargetWithIncompatibleReason_ShouldFailValidation(CollectionReason reason)
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = Guid.NewGuid(),
            WasteBinId = null,
            CollectionReason = reason,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            SchedulingReason = "Discretion justification provided."
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Collection reason must be 'VerifiedReport' when targeting a waste report", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateManualCollectionTaskRequest_BinTargetWithVerifiedReportReason_ShouldFailValidation()
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = null,
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("when targeting a bin", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Tiny")]
    public void CreateManualCollectionTaskRequest_OfficerDiscretionWithoutValidSchedulingReason_ShouldFailValidation(string? justification)
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = null,
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.OfficerDiscretion,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            SchedulingReason = justification
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Scheduling reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateManualCollectionTaskRequest_ScheduledAtInThePast_ShouldFailValidation()
    {
        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = Guid.NewGuid(),
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(-1)
        };

        var result = _createTaskValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot be in the past", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region RescheduleCollectionTaskRequest Tests

    [Fact]
    public void RescheduleCollectionTaskRequest_ValidData_ShouldPassValidation()
    {
        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(6),
            Reason = "Depot vehicle maintenance delayed morning departure."
        };

        var result = _rescheduleValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Reason is required.")]
    [InlineData("Late", "between 5 and 500 characters")]
    public void RescheduleCollectionTaskRequest_InvalidReason_ShouldFailValidation(string reason, string expectedError)
    {
        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(6),
            Reason = reason
        };

        var result = _rescheduleValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RescheduleCollectionTaskRequest_DateInPast_ShouldFailValidation()
    {
        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(-2),
            Reason = "Depot vehicle maintenance delayed departure."
        };

        var result = _rescheduleValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot be in the past", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region CollectionNeedListQuery Tests

    [Fact]
    public void CollectionNeedListQuery_ValidQuery_ShouldPassValidation()
    {
        var query = new CollectionNeedListQuery
        {
            Page = 1,
            PageSize = 20,
            TargetType = "Report",
            CollectionReason = "VerifiedReport",
            WasteType = WasteType.General,
            Search = "Pettah"
        };

        var result = _needQueryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Vehicle")]
    [InlineData("Driver")]
    public void CollectionNeedListQuery_InvalidTargetType_ShouldFailValidation(string invalidTarget)
    {
        var query = new CollectionNeedListQuery
        {
            TargetType = invalidTarget
        };

        var result = _needQueryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("'Report' or 'Bin'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CollectionNeedListQuery_InvalidCollectionReason_ShouldFailValidation()
    {
        var query = new CollectionNeedListQuery
        {
            CollectionReason = "UnknownReason"
        };

        var result = _needQueryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("CollectionReason must be 'VerifiedReport', 'FullOrBlockedBin', or 'RoutineCollection'", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region CollectionTaskListQuery Tests

    [Fact]
    public void CollectionTaskListQuery_ValidQuery_ShouldPassValidation()
    {
        var query = new CollectionTaskListQuery
        {
            Page = 1,
            PageSize = 20,
            Status = CollectionTaskStatus.Scheduled,
            TargetType = "Bin",
            CollectionReason = CollectionReason.FullOrBlockedBin,
            DateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            DateTo = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        var result = _taskListQueryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CollectionTaskListQuery_DateToEarlierThanDateFrom_ShouldFailValidation()
    {
        var query = new CollectionTaskListQuery
        {
            DateFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            DateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
        };

        var result = _taskListQueryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("DateTo cannot be earlier than DateFrom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CollectionTaskListQuery_InvalidTargetType_ShouldFailValidation()
    {
        var query = new CollectionTaskListQuery
        {
            TargetType = "InvalidTarget"
        };

        var result = _taskListQueryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("'Report' or 'Bin'", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
