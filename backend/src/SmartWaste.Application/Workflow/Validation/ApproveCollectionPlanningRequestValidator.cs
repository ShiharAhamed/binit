using FluentValidation;
using SmartWaste.Application.Workflow.DTOs.Requests;

namespace SmartWaste.Application.Workflow.Validation;

/// <summary>
/// Validator for ApproveCollectionPlanningRequest.
/// </summary>
public class ApproveCollectionPlanningRequestValidator : AbstractValidator<ApproveCollectionPlanningRequest>
{
    public ApproveCollectionPlanningRequestValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("ExpectedVersion must be greater than zero.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters.");
    }
}
