using FluentValidation;
using SmartWaste.Application.Reporting.DTOs.Requests;

namespace SmartWaste.Application.Reporting.Validation;

/// <summary>
/// Validator for citizen waste report PATCH update requests.
/// Validates supplied fields only; omitted/null fields are bypassed.
/// </summary>
public class UpdateWasteReportRequestValidator : AbstractValidator<UpdateWasteReportRequest>
{
    public UpdateWasteReportRequestValidator()
    {
        RuleFor(x => x.Description)
            .MinimumLength(10).WithMessage("Description must be at least 10 characters.")
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.WasteType)
            .IsInEnum().WithMessage("A valid waste type must be selected.")
            .When(x => x.WasteType.HasValue);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90.0 and 90.0.")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180.0 and 180.0.")
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x)
            .Must(x => x.Description is not null
                       || x.WasteType.HasValue
                       || x.Latitude.HasValue
                       || x.Longitude.HasValue
                       || x.AddressText is not null)
            .WithMessage("At least one editable field must be provided in the update request.");

        RuleFor(x => x.AddressText)
            .MaximumLength(500).WithMessage("Address text cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.AddressText));
    }
}
