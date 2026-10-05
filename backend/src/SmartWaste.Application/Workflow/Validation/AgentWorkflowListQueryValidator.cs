using FluentValidation;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.Validation;

/// <summary>
/// Validator for AgentWorkflowListQuery enforcing pagination bounds and status filter validity.
/// </summary>
public class AgentWorkflowListQueryValidator : AbstractValidator<AgentWorkflowListQuery>
{
    public AgentWorkflowListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("PageSize must be between 1 and 50.");

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrEmpty(s) || Enum.TryParse<AgentWorkflowStatus>(s, true, out _))
            .WithMessage("Status filter must be a valid AgentWorkflowStatus value (e.g., Created, Planning, Completed).");
    }
}
