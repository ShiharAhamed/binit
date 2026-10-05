using FluentValidation;
using SmartWaste.Application.Workflow.DTOs.Requests;

namespace SmartWaste.Application.Workflow.Validation;

/// <summary>
/// Validator for CreateAgentWorkflowRequest enforcing objective length and format rules.
/// </summary>
public class CreateAgentWorkflowRequestValidator : AbstractValidator<CreateAgentWorkflowRequest>
{
    public CreateAgentWorkflowRequestValidator()
    {
        RuleFor(x => x.Objective)
            .NotEmpty().WithMessage("Objective is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Objective cannot consist solely of whitespace.")
            .MinimumLength(5).WithMessage("Objective must be at least 5 characters long.")
            .MaximumLength(1000).WithMessage("Objective cannot exceed 1000 characters.");
    }
}
