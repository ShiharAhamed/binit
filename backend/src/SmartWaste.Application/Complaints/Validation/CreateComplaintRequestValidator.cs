using FluentValidation;
using SmartWaste.Application.Complaints.DTOs.Requests;

namespace SmartWaste.Application.Complaints.Validation;

/// <summary>
/// Validator for citizen complaint creation requests.
/// Enforces required subject, description, category, and coordinate pairing/range rules.
/// </summary>
public class CreateComplaintRequestValidator : AbstractValidator<CreateComplaintRequest>
{
    public CreateComplaintRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MinimumLength(5).WithMessage("Subject must be at least 5 characters.")
            .MaximumLength(200).WithMessage("Subject cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MinimumLength(10).WithMessage("Description must be at least 10 characters.")
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.");

        RuleFor(x => x.Category)
            .NotNull().WithMessage("Category is required.")
            .IsInEnum().WithMessage("A valid complaint category is required.");

        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("Latitude is required when longitude is provided.")
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("Longitude is required when latitude is provided.")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90.0 and 90.0.")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180.0 and 180.0.")
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x.LocationDescription)
            .MaximumLength(500).WithMessage("Location description cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.LocationDescription));
    }
}
