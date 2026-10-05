using FluentValidation;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for bin administrative deactivation requests.
/// </summary>
public class DeactivateWasteBinRequestValidator : AbstractValidator<DeactivateWasteBinRequest>
{
    public DeactivateWasteBinRequestValidator()
    {
        RuleFor(x => x.TargetStatus)
            .NotNull().WithMessage("Target status is required.")
            .Must(status => status == BinAdministrativeStatus.OutOfService || status == BinAdministrativeStatus.Retired)
            .WithMessage("Target status must be 'OutOfService' or 'Retired'.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
