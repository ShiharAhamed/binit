using FluentValidation;
using SmartWaste.Application.Workflow.DTOs.Requests;

namespace SmartWaste.Application.Workflow.Validation;

/// <summary>
/// FluentValidation validator for ExecuteDispatchPlanRequest.
/// </summary>
public class ExecuteDispatchPlanRequestValidator : AbstractValidator<ExecuteDispatchPlanRequest>
{
    public ExecuteDispatchPlanRequestValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("ExpectedVersion must be greater than 0.");
    }
}
