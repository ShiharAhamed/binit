using FluentAssertions;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Collection.Validation;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Reporting.Enums;
using Xunit;

namespace SmartWaste.Tests.Collection.Validation;

public class WasteBinValidationTests
{
    private readonly CreateWasteBinRequestValidator _createValidator = new();
    private readonly UpdateWasteBinRequestValidator _updateValidator = new();
    private readonly DeactivateWasteBinRequestValidator _deactivateValidator = new();
    private readonly PublicWasteBinQueryValidator _publicQueryValidator = new();
    private readonly WasteBinListQueryValidator _internalQueryValidator = new();

    #region CreateWasteBinRequest Tests

    [Fact]
    public void CreateWasteBinRequest_ValidData_ShouldPassValidation()
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0043",
            Latitude = 6.9312,
            Longitude = 79.8504,
            AddressText = "Galle Face Green Promenade",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Organic },
            CollectionWeekdays = new[] { 1, 3, 5 }
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateWasteBinRequest_EmptyCollectionWeekdays_ShouldPassValidation()
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0044",
            Latitude = 6.9312,
            Longitude = 79.8504,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = Array.Empty<int>()
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Bin code is required.")]
    [InlineData("B", "between 3 and 50 characters")]
    [InlineData("BIN#COL*01", "only alphanumeric characters and hyphens")]
    public void CreateWasteBinRequest_InvalidBinCode_ShouldFailValidation(string binCode, string expectedError)
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = binCode,
            Latitude = 6.9312,
            Longitude = 79.8504,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void CreateWasteBinRequest_InvalidCapacity_ShouldFailValidation(int capacity)
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0045",
            Latitude = 6.9312,
            Longitude = 79.8504,
            CapacityLiters = capacity,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("greater than 0", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-91.0, 79.0, "Latitude must be between -90.0 and 90.0.")]
    [InlineData(91.0, 79.0, "Latitude must be between -90.0 and 90.0.")]
    [InlineData(6.0, -181.0, "Longitude must be between -180.0 and 180.0.")]
    [InlineData(6.0, 181.0, "Longitude must be between -180.0 and 180.0.")]
    public void CreateWasteBinRequest_CoordinatesOutOfBounds_ShouldFailValidation(double lat, double lon, string expectedError)
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0046",
            Latitude = lat,
            Longitude = lon,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteBinRequest_EmptyAcceptedWasteTypes_ShouldFailValidation()
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0047",
            Latitude = 6.9312,
            Longitude = 79.8504,
            CapacityLiters = 1100,
            AcceptedWasteTypes = Array.Empty<WasteType>(),
            CollectionWeekdays = new[] { 1 }
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("At least one accepted waste type is required.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteBinRequest_DuplicateAcceptedWasteTypes_ShouldFailValidation()
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0048",
            Latitude = 6.9312,
            Longitude = 79.8504,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("duplicates", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(new[] { 0, 1 }, "between 1 (Monday) and 7 (Sunday)")]
    [InlineData(new[] { 8 }, "between 1 (Monday) and 7 (Sunday)")]
    [InlineData(new[] { -1 }, "between 1 (Monday) and 7 (Sunday)")]
    public void CreateWasteBinRequest_InvalidWeekdayRange_ShouldFailValidation(int[] weekdays, string expectedError)
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0049",
            Latitude = 6.9312,
            Longitude = 79.8504,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = weekdays
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateWasteBinRequest_DuplicateWeekdays_ShouldFailValidation()
    {
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0050",
            Latitude = 6.9312,
            Longitude = 79.8504,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1, 1, 3 }
        };

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("duplicates", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region UpdateWasteBinRequest Tests

    [Fact]
    public void UpdateWasteBinRequest_ValidData_ShouldPassValidation()
    {
        var request = new UpdateWasteBinRequest
        {
            Latitude = 6.9315,
            Longitude = 79.8506,
            AddressText = "Galle Face Green Promenade (North Pavilion)",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Organic, WasteType.Recyclable },
            CollectionWeekdays = new[] { 1, 3, 5 }
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region DeactivateWasteBinRequest Tests

    [Theory]
    [InlineData(BinAdministrativeStatus.OutOfService)]
    [InlineData(BinAdministrativeStatus.Retired)]
    public void DeactivateWasteBinRequest_ValidStatus_ShouldPassValidation(BinAdministrativeStatus status)
    {
        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = status,
            Reason = "Damaged hinge undergoing depot repair."
        };

        var result = _deactivateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeactivateWasteBinRequest_ActiveStatus_ShouldFailValidation()
    {
        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.Active,
            Reason = "Trying to activate via deactivate"
        };

        var result = _deactivateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("'OutOfService' or 'Retired'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeactivateWasteBinRequest_NullStatus_ShouldFailValidation()
    {
        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = null
        };

        var result = _deactivateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Target status is required", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region PublicWasteBinQuery Tests

    [Fact]
    public void PublicWasteBinQuery_ValidQuery_ShouldPassValidation()
    {
        var query = new PublicWasteBinQuery
        {
            Page = 1,
            PageSize = 20,
            Latitude = 6.9271,
            Longitude = 79.8612,
            RadiusKm = 5.0,
            WasteType = WasteType.General
        };

        var result = _publicQueryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PublicWasteBinQuery_RadiusWithoutCoordinates_ShouldFailValidation()
    {
        var query = new PublicWasteBinQuery
        {
            RadiusKm = 10.0,
            Latitude = null,
            Longitude = null
        };

        var result = _publicQueryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Latitude is required when search radius is specified", StringComparison.OrdinalIgnoreCase));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Longitude is required when search radius is specified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublicWasteBinQuery_PageSizeExceeds50_ShouldFailValidation()
    {
        var query = new PublicWasteBinQuery
        {
            PageSize = 51
        };

        var result = _publicQueryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("between 1 and 50", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region WasteBinListQuery Tests

    [Fact]
    public void WasteBinListQuery_ValidQuery_ShouldPassValidation()
    {
        var query = new WasteBinListQuery
        {
            Page = 1,
            PageSize = 20,
            Status = BinAdministrativeStatus.Active,
            WasteType = WasteType.Recyclable,
            Condition = BinCondition.Good,
            MinFillLevel = 75,
            Search = "Main Street"
        };

        var result = _internalQueryValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WasteBinListQuery_InvalidMinFillLevel_ShouldFailValidation()
    {
        var query = new WasteBinListQuery
        {
            MinFillLevel = 33
        };

        var result = _internalQueryValidator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("0, 25, 50, 75, or 100", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
