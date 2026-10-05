using FluentValidation;
using SmartWaste.Application.Collection.Queries;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for operational collection task list queries.
/// </summary>
public class CollectionTaskListQueryValidator : AbstractValidator<CollectionTaskListQuery>
{
    private static readonly string[] AllowedTargetTypes = { "report", "bin" };

    public CollectionTaskListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid task status filter.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.TargetType)
            .Must(t => string.IsNullOrEmpty(t) || AllowedTargetTypes.Contains(t.ToLowerInvariant()))
            .WithMessage("TargetType must be 'Report' or 'Bin'.")
            .When(x => !string.IsNullOrEmpty(x.TargetType));

        RuleFor(x => x.CollectionReason)
            .IsInEnum().WithMessage("Invalid collection reason filter.")
            .When(x => x.CollectionReason.HasValue);

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom!.Value)
            .WithMessage("DateTo cannot be earlier than DateFrom.")
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue);
    }
}
