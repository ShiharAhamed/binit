using FluentAssertions;
using SmartWaste.Application.Complaints.DTOs.Requests;
using SmartWaste.Application.Complaints.Queries;
using SmartWaste.Application.Complaints.Validation;
using SmartWaste.Domain.Complaints.Enums;
using Xunit;

namespace SmartWaste.Tests.Complaints.Validation;

public class ComplaintValidationTests
{
    private readonly CreateComplaintRequestValidator _createValidator = new();
    private readonly ResolveComplaintRequestValidator _resolveValidator = new();
    private readonly ComplaintListQueryValidator _queryValidator = new();

    #region CreateComplaintRequest Tests

    [Fact]
    public void CreateComplaintRequest_ValidDataWithoutLocation_ShouldPassValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection on Elm Street",
            Description = "The scheduled waste collection was missed on Elm Street this morning."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateComplaintRequest_ValidDataWithCoordinatesAndLocationDescription_ShouldPassValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.PoorService,
            Subject = "Spilled waste during collection",
            Description = "Waste was spilled across the pavement and not cleaned up during morning pickup.",
            Latitude = 6.9271,
            Longitude = 79.8612,
            LocationDescription = "Near Main Street entrance behind municipal market"
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Subject is required.")]
    [InlineData("Abc", "at least 5 characters")]
    public void CreateComplaintRequest_InvalidSubject_ShouldFailValidation(string subject, string expectedError)
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.MissedCollection,
            Subject = subject,
            Description = "Valid description of the missed collection incident on Elm Street."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateComplaintRequest_SubjectExceeds200Chars_ShouldFailValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.MissedCollection,
            Subject = new string('S', 201),
            Description = "Valid description of the missed collection incident on Elm Street."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 200 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("", "Description is required.")]
    [InlineData("Too short", "at least 10 characters")]
    public void CreateComplaintRequest_InvalidDescription_ShouldFailValidation(string description, string expectedError)
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection on Elm Street",
            Description = description
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateComplaintRequest_DescriptionExceeds2000Chars_ShouldFailValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection on Elm Street",
            Description = new string('D', 2001)
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 2000 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateComplaintRequest_MissingCategory_ShouldFailValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = null,
            Subject = "Missed collection on Elm Street",
            Description = "Valid description of the missed collection incident on Elm Street."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Category is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateComplaintRequest_InvalidCategoryEnum_ShouldFailValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = (ComplaintCategory)999,
            Subject = "Missed collection on Elm Street",
            Description = "Valid description of the missed collection incident on Elm Street."
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("valid complaint category", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateComplaintRequest_LatitudeWithoutLongitude_ShouldFailValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.DelayedService,
            Subject = "Delayed service collection",
            Description = "Collection truck arrived over 4 hours late causing road obstruction.",
            Latitude = 6.9271,
            Longitude = null
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Longitude is required when latitude is provided", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateComplaintRequest_LongitudeWithoutLatitude_ShouldFailValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.DelayedService,
            Subject = "Delayed service collection",
            Description = "Collection truck arrived over 4 hours late causing road obstruction.",
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
    [InlineData(-120.0)]
    public void CreateComplaintRequest_LatitudeOutOfRange_ShouldFailValidation(double latitude)
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.DelayedService,
            Subject = "Delayed service collection",
            Description = "Collection truck arrived over 4 hours late causing road obstruction.",
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
    [InlineData(200.0)]
    public void CreateComplaintRequest_LongitudeOutOfRange_ShouldFailValidation(double longitude)
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.DelayedService,
            Subject = "Delayed service collection",
            Description = "Collection truck arrived over 4 hours late causing road obstruction.",
            Latitude = 6.9271,
            Longitude = longitude
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Longitude must be between -180.0 and 180.0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateComplaintRequest_LocationDescriptionExceeds500Chars_ShouldFailValidation()
    {
        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.Other,
            Subject = "General service feedback",
            Description = "Feedback regarding community waste collection point maintenance.",
            LocationDescription = new string('L', 501)
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Location description cannot exceed 500 characters", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region ResolveComplaintRequest Tests

    [Fact]
    public void ResolveComplaintRequest_ValidResolutionNote_ShouldPassValidation()
    {
        var request = new ResolveComplaintRequest
        {
            ResolutionNote = "Dispatched recovery crew to Elm Street and completed collection at 14:30."
        };

        var result = _resolveValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Resolution note is required.")]
    [InlineData("Done", "at least 5 characters")]
    public void ResolveComplaintRequest_MissingOrShortResolutionNote_ShouldFailValidation(string note, string expectedError)
    {
        var request = new ResolveComplaintRequest
        {
            ResolutionNote = note
        };

        var result = _resolveValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveComplaintRequest_ResolutionNoteExceeds1000Chars_ShouldFailValidation()
    {
        var request = new ResolveComplaintRequest
        {
            ResolutionNote = new string('R', 1001)
        };

        var result = _resolveValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 1000 characters", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region ComplaintListQuery Tests

    [Fact]
    public void ComplaintListQuery_DefaultQuery_ShouldPassValidation()
    {
        var query = new ComplaintListQuery();

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ComplaintListQuery_PageLessThanOne_ShouldFailValidation(int page)
    {
        var query = new ComplaintListQuery { Page = page };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Page must be greater than or equal to 1", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void ComplaintListQuery_InvalidPageSize_ShouldFailValidation(int pageSize)
    {
        var query = new ComplaintListQuery { PageSize = pageSize };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Page size must be between 1 and 100", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ComplaintListQuery_InvalidStatusEnum_ShouldFailValidation()
    {
        var query = new ComplaintListQuery { Status = (ComplaintStatus)999 };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid complaint status filter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ComplaintListQuery_InvalidCategoryEnum_ShouldFailValidation()
    {
        var query = new ComplaintListQuery { Category = (ComplaintCategory)999 };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid complaint category filter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ComplaintListQuery_InvalidSortBy_ShouldFailValidation()
    {
        var query = new ComplaintListQuery { SortBy = "invalidField" };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("SortBy must be 'createdAt' or 'updatedAt'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ComplaintListQuery_InvalidSortDirection_ShouldFailValidation()
    {
        var query = new ComplaintListQuery { SortDirection = "diagonal" };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("SortDirection must be 'asc' or 'desc'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ComplaintListQuery_ValidFilterAndPagination_ShouldPassValidation()
    {
        var query = new ComplaintListQuery
        {
            Page = 2,
            PageSize = 50,
            Status = ComplaintStatus.InReview,
            Category = ComplaintCategory.MissedCollection,
            Search = "Elm Street",
            SortBy = "updatedAt",
            SortDirection = "asc"
        };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
