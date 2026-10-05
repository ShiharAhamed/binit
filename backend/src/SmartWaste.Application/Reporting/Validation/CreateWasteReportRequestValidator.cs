using FluentValidation;
using SmartWaste.Application.Reporting.DTOs.Requests;

namespace SmartWaste.Application.Reporting.Validation;

/// <summary>
/// Validator for citizen waste report creation requests.
/// </summary>
public class CreateWasteReportRequestValidator : AbstractValidator<CreateWasteReportRequest>
{
    public CreateWasteReportRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MinimumLength(10).WithMessage("Description must be at least 10 characters.")
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.");

        RuleFor(x => x.WasteType)
            .NotNull().WithMessage("Waste type is required.")
            .IsInEnum().WithMessage("A valid waste type is required.");

        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("Latitude is required.")
            .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90.0 and 90.0.");

        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("Longitude is required.")
            .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180.0 and 180.0.");

        RuleFor(x => x.AddressText)
            .MaximumLength(500).WithMessage("Address text cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.AddressText));
    }
}
