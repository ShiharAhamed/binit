using FluentValidation;
using SmartWaste.Application.Collection.DTOs.Requests;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for task rescheduling requests.
/// </summary>
public class RescheduleCollectionTaskRequestValidator : AbstractValidator<RescheduleCollectionTaskRequest>
{
    public RescheduleCollectionTaskRequestValidator()
    {
        RuleFor(x => x.NewScheduledAt)
            .NotNull().WithMessage("New scheduled time is required.")
            .Must(dt => dt.HasValue && dt.Value >= DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("New scheduled time cannot be in the past.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .Length(5, 500).WithMessage("Reason must be between 5 and 500 characters.");
    }
}
