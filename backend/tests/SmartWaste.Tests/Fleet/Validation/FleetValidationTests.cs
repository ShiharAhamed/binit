using FluentAssertions;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.Queries;
using SmartWaste.Application.Fleet.Validation;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Tests.Collection.Validation;

public class FleetValidationTests
{
    [Fact]
    public void DriverAvailability_DoesNotRequireLicenceOrEligibilityInput()
    {
        var queryValidator = new DriverListQueryValidator();
        queryValidator.Validate(new DriverListQuery { AvailabilityStatus = DriverAvailabilityStatus.Available }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DriverAvailability_RejectsMissingAndUndefinedValues()
    {
        var validator = new UpdateDriverAvailabilityRequestValidator();
        validator.Validate(new UpdateDriverAvailabilityRequest()).IsValid.Should().BeFalse();
        validator.Validate(new UpdateDriverAvailabilityRequest { AvailabilityStatus = (DriverAvailabilityStatus)99 }).IsValid.Should().BeFalse();
        validator.Validate(new UpdateDriverAvailabilityRequest { AvailabilityStatus = DriverAvailabilityStatus.OffDuty }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void VehicleRequest_RequiresPositiveCapacityAndDistinctValidWasteTypes()
    {
        var validator = new CreateVehicleRequestValidator();
        validator.Validate(new CreateVehicleRequest { RegistrationNumber = "V-1", VehicleType = VehicleType.Compactor, CapacityLiters = 0, SupportedWasteTypes = new[] { WasteType.General } }).IsValid.Should().BeFalse();
        validator.Validate(new CreateVehicleRequest { RegistrationNumber = "V-1", VehicleType = VehicleType.Compactor, CapacityLiters = 1, SupportedWasteTypes = new[] { WasteType.General, WasteType.General } }).IsValid.Should().BeFalse();
        validator.Validate(new CreateVehicleRequest { RegistrationNumber = "V-1", VehicleType = VehicleType.Compactor, CapacityLiters = 1, SupportedWasteTypes = new[] { WasteType.General } }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void VehicleStatus_RejectsAssignedAndOtherUndefinedValues()
    {
        var validator = new UpdateVehicleOperationalStatusRequestValidator();
        validator.Validate(new UpdateVehicleOperationalStatusRequest { OperationalStatus = (VehicleOperationalStatus)99 }).IsValid.Should().BeFalse();
        validator.Validate(new UpdateVehicleOperationalStatusRequest { OperationalStatus = VehicleOperationalStatus.Maintenance }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void FleetQueries_EnforceBoundedPaginationAndKnownEnums()
    {
        new VehicleListQueryValidator().Validate(new VehicleListQuery { Page = 0 }).IsValid.Should().BeFalse();
        new DriverListQueryValidator().Validate(new DriverListQuery { PageSize = 101 }).IsValid.Should().BeFalse();
        new VehicleListQueryValidator().Validate(new VehicleListQuery { VehicleType = (VehicleType)99 }).IsValid.Should().BeFalse();
    }
}
