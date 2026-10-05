using FluentValidation;
using SmartWaste.Application.Workflow.DTOs.Requests;

namespace SmartWaste.Application.Workflow.Validation;

/// <summary>
/// Validator for ApproveDispatchPlanRequest.
/// </summary>
public class ApproveDispatchPlanRequestValidator : AbstractValidator<ApproveDispatchPlanRequest>
{
    public ApproveDispatchPlanRequestValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("ExpectedVersion must be greater than zero.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters.");
    }
}
