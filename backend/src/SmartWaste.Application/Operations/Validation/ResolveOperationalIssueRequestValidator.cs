using FluentValidation;
using SmartWaste.Application.Operations.DTOs.Requests;

namespace SmartWaste.Application.Operations.Validation;

/// <summary>
/// Validator for operational issue resolution requests.
/// </summary>
public class ResolveOperationalIssueRequestValidator : AbstractValidator<ResolveOperationalIssueRequest>
{
    public ResolveOperationalIssueRequestValidator()
    {
        RuleFor(x => x.ResolutionNote)
            .NotEmpty().WithMessage("Resolution note is required.")
            .MinimumLength(5).WithMessage("Resolution note must be at least 5 characters.")
            .MaximumLength(1000).WithMessage("Resolution note cannot exceed 1000 characters.");
    }
}
