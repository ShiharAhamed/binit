using FluentValidation;
using SmartWaste.Application.Collection.Queries;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for internal staff bin list queries.
/// </summary>
public class WasteBinListQueryValidator : AbstractValidator<WasteBinListQuery>
{
    private static readonly int[] AllowedFillLevels = { 0, 25, 50, 75, 100 };

    public WasteBinListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid administrative status filter.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.WasteType)
            .IsInEnum().WithMessage("Invalid waste type filter.")
            .When(x => x.WasteType.HasValue);

        RuleFor(x => x.Condition)
            .IsInEnum().WithMessage("Invalid bin condition filter.")
            .When(x => x.Condition.HasValue);

        RuleFor(x => x.MinFillLevel)
            .Must(level => level.HasValue && AllowedFillLevels.Contains(level.Value))
            .WithMessage("MinFillLevel must be 0, 25, 50, 75, or 100.")
            .When(x => x.MinFillLevel.HasValue);
    }
}
