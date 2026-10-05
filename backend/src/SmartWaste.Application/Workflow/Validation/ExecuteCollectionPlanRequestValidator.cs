using FluentValidation;
using SmartWaste.Application.Workflow.DTOs.Requests;

namespace SmartWaste.Application.Workflow.Validation;

/// <summary>
/// FluentValidation validator for ExecuteCollectionPlanRequest.
/// </summary>
public class ExecuteCollectionPlanRequestValidator : AbstractValidator<ExecuteCollectionPlanRequest>
{
    public ExecuteCollectionPlanRequestValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("ExpectedVersion must be greater than 0.");
    }
}
