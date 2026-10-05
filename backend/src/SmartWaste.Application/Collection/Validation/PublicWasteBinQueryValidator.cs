using FluentValidation;
using SmartWaste.Application.Collection.Queries;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for citizen public roadside bin discovery queries.
/// </summary>
public class PublicWasteBinQueryValidator : AbstractValidator<PublicWasteBinQuery>
{
    public PublicWasteBinQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.RadiusKm)
            .InclusiveBetween(0.1, 50.0).WithMessage("Radius must be between 0.1 and 50.0 km.")
            .When(x => x.RadiusKm.HasValue);

        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("Latitude is required when search radius is specified.")
            .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90.0 and 90.0.")
            .When(x => x.RadiusKm.HasValue);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90.0 and 90.0.")
            .When(x => !x.RadiusKm.HasValue && x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("Longitude is required when search radius is specified.")
            .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180.0 and 180.0.")
            .When(x => x.RadiusKm.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180.0 and 180.0.")
            .When(x => !x.RadiusKm.HasValue && x.Longitude.HasValue);

        RuleFor(x => x.WasteType)
            .IsInEnum().WithMessage("Invalid waste type filter.")
            .When(x => x.WasteType.HasValue);
    }
}
