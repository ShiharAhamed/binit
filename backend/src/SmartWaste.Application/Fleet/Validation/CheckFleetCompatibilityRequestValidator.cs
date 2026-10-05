using FluentValidation;
using SmartWaste.Application.Fleet.DTOs.Requests;

namespace SmartWaste.Application.Fleet.Validation;

/// <summary>
/// Validates input for the internal AI fleet compatibility checking tool.
/// </summary>
public class CheckFleetCompatibilityRequestValidator : AbstractValidator<CheckFleetCompatibilityRequest>
{
    public CheckFleetCompatibilityRequestValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty()
            .WithMessage("VehicleId must not be empty.");

        RuleFor(x => x.TaskIds)
            .NotEmpty()
            .WithMessage("TaskIds must contain at least one task ID.")
            .Must(x => x.Distinct().Count() == x.Count)
            .WithMessage("TaskIds must not contain duplicate task IDs.")
            .Must(x => x.Count <= 50)
            .WithMessage("TaskIds cannot exceed 50 tasks per check.");

        RuleForEach(x => x.TaskIds)
            .NotEmpty()
            .WithMessage("Task ID cannot be empty.");
    }
}
