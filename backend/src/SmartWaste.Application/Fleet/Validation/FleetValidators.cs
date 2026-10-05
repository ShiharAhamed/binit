using FluentValidation;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.Queries;

namespace SmartWaste.Application.Fleet.Validation;

public class UpdateDriverAvailabilityRequestValidator : AbstractValidator<UpdateDriverAvailabilityRequest>
{
    public UpdateDriverAvailabilityRequestValidator() => RuleFor(x => x.AvailabilityStatus).NotNull().IsInEnum();
}

public class CreateVehicleRequestValidator : AbstractValidator<CreateVehicleRequest>
{
    public CreateVehicleRequestValidator()
    {
        RuleFor(x => x.RegistrationNumber).NotEmpty().Length(1, 50);
        RuleFor(x => x.VehicleType).NotNull().IsInEnum();
        RuleFor(x => x.CapacityLiters).NotNull().GreaterThan(0);
        RuleFor(x => x.SupportedWasteTypes).NotNull().NotEmpty().Must(x => x is null || x.Distinct().Count() == x.Count);
        RuleForEach(x => x.SupportedWasteTypes).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

public class UpdateVehicleRequestValidator : AbstractValidator<UpdateVehicleRequest>
{
    public UpdateVehicleRequestValidator() => Include(new CreateVehicleRequestValidator());
}

public class UpdateVehicleOperationalStatusRequestValidator : AbstractValidator<UpdateVehicleOperationalStatusRequest>
{
    public UpdateVehicleOperationalStatusRequestValidator() => RuleFor(x => x.OperationalStatus).NotNull().IsInEnum();
}

public class DriverListQueryValidator : AbstractValidator<DriverListQuery>
{
    public DriverListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.AvailabilityStatus).IsInEnum().When(x => x.AvailabilityStatus.HasValue);
    }
}

public class VehicleListQueryValidator : AbstractValidator<VehicleListQuery>
{
    public VehicleListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.OperationalStatus).IsInEnum().When(x => x.OperationalStatus.HasValue);
        RuleFor(x => x.VehicleType).IsInEnum().When(x => x.VehicleType.HasValue);
    }
}
