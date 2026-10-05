using FluentValidation;
using SmartWaste.Application.Reporting.Queries;

namespace SmartWaste.Application.Reporting.Validation;

/// <summary>
/// Validator for waste report query, filter, sort, and pagination parameters.
/// </summary>
public class WasteReportListQueryValidator : AbstractValidator<WasteReportListQuery>
{
    private static readonly string[] AllowedSortBy = { "createdat", "updatedat" };
    private static readonly string[] AllowedSortDirection = { "asc", "desc" };

    public WasteReportListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid report status filter.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.WasteType)
            .IsInEnum().WithMessage("Invalid waste type filter.")
            .When(x => x.WasteType.HasValue);

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrEmpty(sortBy) || AllowedSortBy.Contains(sortBy.ToLowerInvariant()))
            .WithMessage("SortBy must be 'createdAt' or 'updatedAt'.");

        RuleFor(x => x.SortDirection)
            .Must(dir => string.IsNullOrEmpty(dir) || AllowedSortDirection.Contains(dir.ToLowerInvariant()))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");

        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .WithMessage("ToDate cannot be earlier than FromDate.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
    }
}
