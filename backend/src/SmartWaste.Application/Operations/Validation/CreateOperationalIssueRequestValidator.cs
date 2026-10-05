using FluentValidation;
using SmartWaste.Application.Operations.DTOs.Requests;

namespace SmartWaste.Application.Operations.Validation;

/// <summary>
/// Validator for driver operational issue creation requests.
/// Enforces required title, description, issue type, and coordinate pairing/range rules.
/// </summary>
public class CreateOperationalIssueRequestValidator : AbstractValidator<CreateOperationalIssueRequest>
{
    public CreateOperationalIssueRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MinimumLength(5).WithMessage("Title must be at least 5 characters.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MinimumLength(10).WithMessage("Description must be at least 10 characters.")
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.");

        RuleFor(x => x.IssueType)
            .NotNull().WithMessage("Issue type is required.")
            .IsInEnum().WithMessage("A valid operational issue type is required.");

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
