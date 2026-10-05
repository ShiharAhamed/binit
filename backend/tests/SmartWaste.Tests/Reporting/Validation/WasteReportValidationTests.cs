using FluentAssertions;
using SmartWaste.Application.Reporting.DTOs.Requests;
using SmartWaste.Application.Reporting.Queries;
using SmartWaste.Application.Reporting.Validation;
using SmartWaste.Domain.Reporting.Enums;
using Xunit;

namespace SmartWaste.Tests.Reporting.Validation;

public class WasteReportValidationTests
{
    private readonly CreateWasteReportRequestValidator _createValidator = new();
    private readonly UpdateWasteReportRequestValidator _updateValidator = new();
    private readonly RejectWasteReportRequestValidator _rejectValidator = new();
    private readonly WasteReportListQueryValidator _queryValidator = new();

    #region CreateWasteReportRequest Tests

    [Fact]
    public void CreateWasteReportRequest_ValidData_ShouldPassValidation()
    {
        var request = new CreateWasteReportRequest
        {
            Description = "Large pile of garbage dumped near road junction",
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Main Street, Pettah"
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Description is required.")]
    [InlineData("Too short", "at least 10 characters")]
    public void CreateWasteReportRequest_InvalidDescription_ShouldFailValidation(string description, string expectedError)
    {
        var request = new CreateWasteReportRequest
        {
            Description = description,
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteReportRequest_DescriptionExceeds1000Chars_ShouldFailValidation()
    {
        var request = new CreateWasteReportRequest
        {
            Description = new string('A', 1001),
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 1000 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    [InlineData(-150.0)]
    public void CreateWasteReportRequest_LatitudeOutOfRange_ShouldFailValidation(double latitude)
    {
        var request = new CreateWasteReportRequest
        {
            Description = "Valid description of the overflowing dump site",
            WasteType = WasteType.General,
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
    public void CreateWasteReportRequest_LongitudeOutOfRange_ShouldFailValidation(double longitude)
    {
        var request = new CreateWasteReportRequest
        {
            Description = "Valid description of the overflowing dump site",
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = longitude
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Longitude must be between -180.0 and 180.0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteReportRequest_AddressTextExceeds500Chars_ShouldFailValidation()
    {
        var request = new CreateWasteReportRequest
        {
            Description = "Valid description of the overflowing dump site",
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = new string('B', 501)
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Address text cannot exceed 500 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteReportRequest_InvalidWasteTypeEnum_ShouldFailValidation()
    {
        var request = new CreateWasteReportRequest
        {
            Description = "Valid description of the overflowing dump site",
            WasteType = (WasteType)999,
            Latitude = 6.9271,
            Longitude = 79.8612
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("valid waste type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteReportRequest_MissingWasteType_ShouldFailValidation()
    {
        var request = new CreateWasteReportRequest
        {
            Description = "Valid description of the overflowing dump site",
            WasteType = null,
            Latitude = 6.9271,
            Longitude = 79.8612
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Waste type is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteReportRequest_MissingLatitude_ShouldFailValidation()
    {
        var request = new CreateWasteReportRequest
        {
            Description = "Valid description of the overflowing dump site",
            WasteType = WasteType.General,
            Latitude = null,
            Longitude = 79.8612
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Latitude is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteReportRequest_MissingLongitude_ShouldFailValidation()
    {
        var request = new CreateWasteReportRequest
        {
            Description = "Valid description of the overflowing dump site",
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = null
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Longitude is required", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region UpdateWasteReportRequest Tests

    [Fact]
    public void UpdateWasteReportRequest_SuppliedValidFields_ShouldPassValidation()
    {
        var request = new UpdateWasteReportRequest
        {
            Description = "Updated: Garbage heap is now extending into street lane",
            WasteType = WasteType.Recyclable,
            Latitude = 6.9275,
            Longitude = 79.8615,
            AddressText = "Updated landmark description"
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateWasteReportRequest_OmittedFields_ShouldPassValidation()
    {
        var request = new UpdateWasteReportRequest
        {
            Description = "Only description is being updated here"
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateWasteReportRequest_InvalidSuppliedDescription_ShouldFailValidation()
    {
        var request = new UpdateWasteReportRequest
        {
            Description = "Short"
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("at least 10 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-95.0, 79.8612)]
    [InlineData(6.9271, -190.0)]
    public void UpdateWasteReportRequest_InvalidSuppliedCoordinates_ShouldFailValidation(double lat, double lng)
    {
        var request = new UpdateWasteReportRequest
        {
            Latitude = lat,
            Longitude = lng
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateWasteReportRequest_OversizedAddress_ShouldFailValidation()
    {
        var request = new UpdateWasteReportRequest
        {
            AddressText = new string('X', 501)
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Address text cannot exceed 500 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpdateWasteReportRequest_EmptyRequest_ShouldFailValidation()
    {
        var request = new UpdateWasteReportRequest();

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("At least one editable field must be provided", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpdateWasteReportRequest_EmptyAddressToClear_ShouldPassValidation()
    {
        var request = new UpdateWasteReportRequest
        {
            AddressText = ""
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region RejectWasteReportRequest Tests

    [Fact]
    public void RejectWasteReportRequest_ValidReason_ShouldPassValidation()
    {
        var request = new RejectWasteReportRequest
        {
            Reason = "Duplicate report already covered by scheduled morning pickup route."
        };

        var result = _rejectValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Rejection reason is required.")]
    [InlineData("No", "at least 5 characters")]
    public void RejectWasteReportRequest_MissingOrShortReason_ShouldFailValidation(string reason, string expectedError)
    {
        var request = new RejectWasteReportRequest
        {
            Reason = reason
        };

        var result = _rejectValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RejectWasteReportRequest_ReasonExceeds500Chars_ShouldFailValidation()
    {
        var request = new RejectWasteReportRequest
        {
            Reason = new string('R', 501)
        };

        var result = _rejectValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cannot exceed 500 characters", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region WasteReportListQuery Tests

    [Fact]
    public void WasteReportListQuery_ValidDefaultQuery_ShouldPassValidation()
    {
        var query = new WasteReportListQuery();

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WasteReportListQuery_PageLessThanOne_ShouldFailValidation(int page)
    {
        var query = new WasteReportListQuery { Page = page };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Page must be greater than or equal to 1", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void WasteReportListQuery_InvalidPageSize_ShouldFailValidation(int pageSize)
    {
        var query = new WasteReportListQuery { PageSize = pageSize };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Page size must be between 1 and 100", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WasteReportListQuery_InvalidSortBy_ShouldFailValidation()
    {
        var query = new WasteReportListQuery { SortBy = "invalidField" };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("SortBy must be 'createdAt' or 'updatedAt'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WasteReportListQuery_InvalidSortDirection_ShouldFailValidation()
    {
        var query = new WasteReportListQuery { SortDirection = "diagonal" };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("SortDirection must be 'asc' or 'desc'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WasteReportListQuery_ToDateEarlierThanFromDate_ShouldFailValidation()
    {
        var query = new WasteReportListQuery
        {
            FromDate = DateTime.UtcNow,
            ToDate = DateTime.UtcNow.AddDays(-1)
        };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("ToDate cannot be earlier than FromDate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WasteReportListQuery_ValidFilterAndDateRange_ShouldPassValidation()
    {
        var query = new WasteReportListQuery
        {
            Page = 2,
            PageSize = 50,
            Status = WasteReportStatus.UnderReview,
            WasteType = WasteType.Hazardous,
            SortBy = "updatedAt",
            SortDirection = "asc",
            FromDate = DateTime.UtcNow.AddDays(-7),
            ToDate = DateTime.UtcNow
        };

        var result = _queryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
