using FluentValidation;
using SmartWaste.Application.Collection.DTOs.Requests;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for bin metadata update requests.
/// </summary>
public class UpdateWasteBinRequestValidator : AbstractValidator<UpdateWasteBinRequest>
{
    public UpdateWasteBinRequestValidator()
    {
        RuleFor(x => x.CapacityLiters)
            .NotNull().WithMessage("Capacity is required.")
            .GreaterThan(0).WithMessage("Capacity must be greater than 0 liters.");

        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("Latitude is required.")
            .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90.0 and 90.0.");

        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("Longitude is required.")
            .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180.0 and 180.0.");

        RuleFor(x => x.AddressText)
            .MaximumLength(500).WithMessage("Address text cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.AddressText));

        RuleFor(x => x.AcceptedWasteTypes)
            .NotNull().WithMessage("Accepted waste types list is required.")
            .NotEmpty().WithMessage("At least one accepted waste type is required.")
            .Must(types => types == null || types.Distinct().Count() == types.Count)
            .WithMessage("Accepted waste types must not contain duplicates.");

        RuleForEach(x => x.AcceptedWasteTypes)
            .IsInEnum().WithMessage("Invalid accepted waste type.");

        RuleFor(x => x.CollectionWeekdays)
            .NotNull().WithMessage("Collection weekdays list is required.")
            .Must(days => days == null || days.All(d => d >= 1 && d <= 7))
            .WithMessage("Collection weekdays must be between 1 (Monday) and 7 (Sunday).")
            .Must(days => days == null || days.Distinct().Count() == days.Count)
            .WithMessage("Collection weekdays must not contain duplicates.");
    }
}
