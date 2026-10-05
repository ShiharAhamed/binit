using FluentValidation;
using SmartWaste.Application.Complaints.DTOs.Requests;

namespace SmartWaste.Application.Complaints.Validation;

/// <summary>
/// Validator for complaint resolution requests.
/// </summary>
public class ResolveComplaintRequestValidator : AbstractValidator<ResolveComplaintRequest>
{
    public ResolveComplaintRequestValidator()
    {
        RuleFor(x => x.ResolutionNote)
            .NotEmpty().WithMessage("Resolution note is required.")
            .MinimumLength(5).WithMessage("Resolution note must be at least 5 characters.")
            .MaximumLength(1000).WithMessage("Resolution note cannot exceed 1000 characters.");
    }
}
