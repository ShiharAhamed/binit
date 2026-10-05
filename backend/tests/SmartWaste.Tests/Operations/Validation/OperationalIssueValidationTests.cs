using FluentAssertions;
using SmartWaste.Application.Operations.DTOs.Requests;
using SmartWaste.Application.Operations.Queries;
using SmartWaste.Application.Operations.Validation;
using SmartWaste.Domain.Operations.Enums;
using Xunit;

namespace SmartWaste.Tests.Operations.Validation;

public class OperationalIssueValidationTests
{
    private readonly CreateOperationalIssueRequestValidator _createValidator = new();
    private readonly ResolveOperationalIssueRequestValidator _resolveValidator = new();
    private readonly OperationalIssueListQueryValidator _queryValidator = new();

    #region CreateOperationalIssueRequest Tests

    [Fact]
    public void CreateOperationalIssueRequest_ValidDataWithoutLocation_ShouldPassValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Hydraulic lift failure",
            Description = "The rear hydraulic arm has reduced lifting capacity and is leaking fluid."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateOperationalIssueRequest_ValidDataWithCoordinatesAndLocationDescription_ShouldPassValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.RoadOrAccessIssue,
            Title = "Road blocked by construction",
            Description = "Road works have completely closed off the access lane to bins 104 and 105.",
            Latitude = 6.9271,
            Longitude = 79.8612,
            LocationDescription = "Access road beside the central school gate"
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Title is required.")]
    [InlineData("Lift", "at least 5 characters")]
    public void CreateOperationalIssueRequest_InvalidTitle_ShouldFailValidation(string title, string expectedError)
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.VehicleProblem,
            Title = title,
            Description = "Valid description of the operational issue observed during collection."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateOperationalIssueRequest_TitleExceeds200Chars_ShouldFailValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.VehicleProblem,
            Title = new string('T', 201),
            Description = "Valid description of the operational issue observed during collection."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 200 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("", "Description is required.")]
    [InlineData("Too short", "at least 10 characters")]
    public void CreateOperationalIssueRequest_InvalidDescription_ShouldFailValidation(string description, string expectedError)
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.EquipmentProblem,
            Title = "Broken compactor mechanism",
            Description = description
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateOperationalIssueRequest_DescriptionExceeds2000Chars_ShouldFailValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.EquipmentProblem,
            Title = "Broken compactor mechanism",
            Description = new string('D', 2001)
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 2000 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateOperationalIssueRequest_MissingIssueType_ShouldFailValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = null,
            Title = "Compactor mechanism malfunction",
            Description = "Compactor blade stops moving midway during dense waste compaction cycle."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Issue type is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateOperationalIssueRequest_InvalidIssueTypeEnum_ShouldFailValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = (OperationalIssueType)999,
            Title = "Compactor mechanism malfunction",
            Description = "Compactor blade stops moving midway during dense waste compaction cycle."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("valid operational issue type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateOperationalIssueRequest_LatitudeWithoutLongitude_ShouldFailValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.SafetyConcern,
            Title = "Overhanging power line risk",
            Description = "Low hanging electrical cable observed near roadside collection point.",
            Latitude = 6.9271,
            Longitude = null
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Longitude is required when latitude is provided", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateOperationalIssueRequest_LongitudeWithoutLatitude_ShouldFailValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.SafetyConcern,
            Title = "Overhanging power line risk",
            Description = "Low hanging electrical cable observed near roadside collection point.",
            Latitude = null,
            Longitude = 79.8612
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Latitude is required when longitude is provided", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    [InlineData(-130.0)]
    public void CreateOperationalIssueRequest_LatitudeOutOfRange_ShouldFailValidation(double latitude)
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.OperationalDelay,
            Title = "Severe traffic congestion",
            Description = "Truck stuck in standstill traffic along major arterial road for 90 minutes.",
            Latitude = latitude,
            Longitude = 79.8612
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Latitude must be between -90.0 and 90.0", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    [InlineData(250.0)]
    public void CreateOperationalIssueRequest_LongitudeOutOfRange_ShouldFailValidation(double longitude)
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.OperationalDelay,
            Title = "Severe traffic congestion",
            Description = "Truck stuck in standstill traffic along major arterial road for 90 minutes.",
            Latitude = 6.9271,
            Longitude = longitude
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Longitude must be between -180.0 and 180.0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateOperationalIssueRequest_LocationDescriptionExceeds500Chars_ShouldFailValidation()
    {
        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.Other,
            Title = "Miscellaneous operational delay",
            Description = "Driver encountered unexpected roadblock due to local festival celebration.",
            LocationDescription = new string('L', 501)
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Location description cannot exceed 500 characters", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region ResolveOperationalIssueRequest Tests

    [Fact]
    public void ResolveOperationalIssueRequest_ValidResolutionNote_ShouldPassValidation()
    {
        var request = new ResolveOperationalIssueRequest
        {
            ResolutionNote = "Mobile maintenance team deployed and repaired hydraulic pump on site."
        };

        var result = _resolveValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Resolution note is required.")]
    [InlineData("Fix", "at least 5 characters")]
    public void ResolveOperationalIssueRequest_MissingOrShortResolutionNote_ShouldFailValidation(string note, string expectedError)
    {
        var request = new ResolveOperationalIssueRequest
        {
            ResolutionNote = note
        };

        var result = _resolveValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveOperationalIssueRequest_ResolutionNoteExceeds1000Chars_ShouldFailValidation()
    {
        var request = new ResolveOperationalIssueRequest
        {
            ResolutionNote = new string('R', 1001)
        };

        var result = _resolveValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 1000 characters", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region OperationalIssueListQuery Tests

    [Fact]
    public void OperationalIssueListQuery_DefaultQuery_ShouldPassValidation()
    {
        var query = new OperationalIssueListQuery();

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OperationalIssueListQuery_PageLessThanOne_ShouldFailValidation(int page)
    {
        var query = new OperationalIssueListQuery { Page = page };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Page must be greater than or equal to 1", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void OperationalIssueListQuery_InvalidPageSize_ShouldFailValidation(int pageSize)
    {
        var query = new OperationalIssueListQuery { PageSize = pageSize };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Page size must be between 1 and 100", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OperationalIssueListQuery_InvalidStatusEnum_ShouldFailValidation()
    {
        var query = new OperationalIssueListQuery { Status = (OperationalIssueStatus)999 };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid operational issue status filter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OperationalIssueListQuery_InvalidIssueTypeEnum_ShouldFailValidation()
    {
        var query = new OperationalIssueListQuery { IssueType = (OperationalIssueType)999 };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid operational issue type filter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OperationalIssueListQuery_InvalidSortBy_ShouldFailValidation()
    {
        var query = new OperationalIssueListQuery { SortBy = "invalidField" };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("SortBy must be 'createdAt' or 'updatedAt'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OperationalIssueListQuery_InvalidSortDirection_ShouldFailValidation()
    {
        var query = new OperationalIssueListQuery { SortDirection = "diagonal" };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("SortDirection must be 'asc' or 'desc'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OperationalIssueListQuery_ValidFilterAndPagination_ShouldPassValidation()
    {
        var query = new OperationalIssueListQuery
        {
            Page = 3,
            PageSize = 25,
            Status = OperationalIssueStatus.InReview,
            IssueType = OperationalIssueType.VehicleProblem,
            Search = "hydraulic",
            SortBy = "createdAt",
            SortDirection = "desc"
        };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
