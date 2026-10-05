using FluentValidation;
using SmartWaste.Application.Workflow.DTOs.Requests;

namespace SmartWaste.Application.Workflow.Validation;

/// <summary>
/// Validator for RequestDispatchRevisionRequest.
/// </summary>
public class RequestDispatchRevisionRequestValidator : AbstractValidator<RequestDispatchRevisionRequest>
{
    public RequestDispatchRevisionRequestValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("ExpectedVersion must be greater than zero.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Reason cannot consist solely of whitespace.")
            .MinimumLength(5)
            .WithMessage("Reason must be at least 5 characters long.")
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters.");
    }
}
