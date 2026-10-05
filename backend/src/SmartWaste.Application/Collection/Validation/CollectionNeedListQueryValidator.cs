using FluentValidation;
using SmartWaste.Application.Collection.Queries;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for unified collection needs queue query parameters.
/// </summary>
public class CollectionNeedListQueryValidator : AbstractValidator<CollectionNeedListQuery>
{
    private static readonly string[] AllowedTargetTypes = { "report", "bin" };
    private static readonly string[] AllowedReasons = { "verifiedreport", "fullorblockedbin", "routinecollection" };

    public CollectionNeedListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.TargetType)
            .Must(t => string.IsNullOrEmpty(t) || AllowedTargetTypes.Contains(t.ToLowerInvariant()))
            .WithMessage("TargetType must be 'Report' or 'Bin'.")
            .When(x => !string.IsNullOrEmpty(x.TargetType));

        RuleFor(x => x.CollectionReason)
            .Must(r => string.IsNullOrEmpty(r) || AllowedReasons.Contains(r.ToLowerInvariant()))
            .WithMessage("CollectionReason must be 'VerifiedReport', 'FullOrBlockedBin', or 'RoutineCollection'.")
            .When(x => !string.IsNullOrEmpty(x.CollectionReason));

        RuleFor(x => x.WasteType)
            .IsInEnum().WithMessage("Invalid waste type filter.")
            .When(x => x.WasteType.HasValue);
    }
}
