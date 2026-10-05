using FluentValidation;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for manual collection task creation requests by WasteOfficers.
/// Enforces target XOR, reason-target compatibility, and discretion justification rules.
/// </summary>
public class CreateManualCollectionTaskRequestValidator : AbstractValidator<CreateManualCollectionTaskRequest>
{
    public CreateManualCollectionTaskRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.WasteReportId.HasValue && !x.WasteBinId.HasValue) || (!x.WasteReportId.HasValue && x.WasteBinId.HasValue))
            .WithMessage("Exactly one of WasteReportId or WasteBinId must be supplied.");

        RuleFor(x => x.CollectionReason)
            .NotNull().WithMessage("Collection reason is required.")
            .IsInEnum().WithMessage("A valid collection reason is required.");

        RuleFor(x => x.CollectionReason)
            .Equal(CollectionReason.VerifiedReport)
            .When(x => x.WasteReportId.HasValue)
            .WithMessage("Collection reason must be 'VerifiedReport' when targeting a waste report.");

        RuleFor(x => x.CollectionReason)
            .Must(r => r == CollectionReason.FullOrBlockedBin || r == CollectionReason.RoutineCollection || r == CollectionReason.OfficerDiscretion)
            .When(x => x.WasteBinId.HasValue)
            .WithMessage("Collection reason must be 'FullOrBlockedBin', 'RoutineCollection', or 'OfficerDiscretion' when targeting a bin.");

        RuleFor(x => x.SchedulingReason)
            .NotEmpty().WithMessage("Scheduling reason is required when collection reason is OfficerDiscretion.")
            .Length(5, 500).WithMessage("Scheduling reason must be between 5 and 500 characters.")
            .When(x => x.CollectionReason == CollectionReason.OfficerDiscretion);

        RuleFor(x => x.SchedulingReason)
            .MaximumLength(500).WithMessage("Scheduling reason cannot exceed 500 characters.")
            .When(x => x.CollectionReason != CollectionReason.OfficerDiscretion && !string.IsNullOrEmpty(x.SchedulingReason));

        RuleFor(x => x.HandlingNotes)
            .MaximumLength(1000).WithMessage("Handling notes cannot exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.HandlingNotes));

        RuleFor(x => x.ScheduledAt)
            .NotNull().WithMessage("Scheduled time is required.")
            .Must(dt => dt.HasValue && dt.Value >= DateTime.UtcNow)
            .WithMessage("Scheduled time cannot be in the past.");
    }
}
