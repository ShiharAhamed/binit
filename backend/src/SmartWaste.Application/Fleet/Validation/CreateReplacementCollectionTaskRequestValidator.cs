using FluentValidation;
using SmartWaste.Application.Collection.DTOs.Requests;

namespace SmartWaste.Application.Collection.Validation;

public sealed class CreateReplacementCollectionTaskRequestValidator : AbstractValidator<CreateReplacementCollectionTaskRequest>
{
    public CreateReplacementCollectionTaskRequestValidator()
    {
        RuleFor(x => x.ScheduledAt).NotNull().Must(x => x.HasValue && x.Value >= DateTime.UtcNow).WithMessage("Scheduled time cannot be in the past.");
        RuleFor(x => x.ReplacementReason).NotEmpty().Length(5, 500);
        RuleFor(x => x.HandlingNotes).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.HandlingNotes));
        RuleFor(x => x.SchedulingReason).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.SchedulingReason));
    }
}
