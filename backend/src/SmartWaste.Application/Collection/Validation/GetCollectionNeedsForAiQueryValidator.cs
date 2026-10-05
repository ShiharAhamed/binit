using FluentValidation;
using SmartWaste.Application.Collection.Queries;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validates the strictly bounded query surface for the internal AI collection-needs tool.
/// </summary>
public class GetCollectionNeedsForAiQueryValidator : AbstractValidator<GetCollectionNeedsForAiQuery>
{
    private static readonly string[] AllowedTargetTypes = ["report", "bin"];
    private static readonly string[] AllowedReasons = ["verifiedreport", "fullorblockedbin", "routinecollection"];

    public GetCollectionNeedsForAiQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("PageSize must be between 1 and 50.");

        RuleFor(x => x.TargetType)
            .Must(value => string.IsNullOrEmpty(value) || AllowedTargetTypes.Contains(value.ToLowerInvariant()))
            .WithMessage("TargetType must be 'Report' or 'Bin'.")
            .When(x => !string.IsNullOrEmpty(x.TargetType));

        RuleFor(x => x.CollectionReason)
            .Must(value => string.IsNullOrEmpty(value) || AllowedReasons.Contains(value.ToLowerInvariant()))
            .WithMessage("CollectionReason must be 'VerifiedReport', 'FullOrBlockedBin', or 'RoutineCollection'.")
            .When(x => !string.IsNullOrEmpty(x.CollectionReason));
    }
}
