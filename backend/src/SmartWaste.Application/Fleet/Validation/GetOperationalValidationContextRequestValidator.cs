using FluentValidation;
using SmartWaste.Application.Fleet.DTOs.Requests;

namespace SmartWaste.Application.Fleet.Validation;

/// <summary>
/// Validates input for the internal AI operational validation context retrieval tool.
/// </summary>
public class GetOperationalValidationContextRequestValidator : AbstractValidator<GetOperationalValidationContextRequest>
{
    public GetOperationalValidationContextRequestValidator()
    {
        RuleFor(x => x.TaskIds)
            .NotNull()
            .WithMessage("TaskIds list cannot be null.")
            .Must(x => x == null || x.Distinct().Count() == x.Count)
            .WithMessage("TaskIds must not contain duplicate task IDs.")
            .Must(x => x == null || x.Count <= 100)
            .WithMessage("TaskIds cannot exceed 100 tasks per validation check.");

        RuleForEach(x => x.TaskIds)
            .NotEmpty()
            .WithMessage("Task ID cannot be empty.");

        RuleFor(x => x.DriverIds)
            .NotNull()
            .WithMessage("DriverIds list cannot be null.")
            .Must(x => x == null || x.Distinct().Count() == x.Count)
            .WithMessage("DriverIds must not contain duplicate driver IDs.")
            .Must(x => x == null || x.Count <= 50)
            .WithMessage("DriverIds cannot exceed 50 drivers per validation check.");

        RuleForEach(x => x.DriverIds)
            .NotEmpty()
            .WithMessage("Driver ID cannot be empty.");

        RuleFor(x => x.VehicleIds)
            .NotNull()
            .WithMessage("VehicleIds list cannot be null.")
            .Must(x => x == null || x.Distinct().Count() == x.Count)
            .WithMessage("VehicleIds must not contain duplicate vehicle IDs.")
            .Must(x => x == null || x.Count <= 50)
            .WithMessage("VehicleIds cannot exceed 50 vehicles per validation check.");

        RuleForEach(x => x.VehicleIds)
            .NotEmpty()
            .WithMessage("Vehicle ID cannot be empty.");
    }
}
