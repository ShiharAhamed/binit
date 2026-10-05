using FluentValidation;
using SmartWaste.Application.Complaints.Queries;

namespace SmartWaste.Application.Complaints.Validation;

/// <summary>
/// Validator for complaint listing and pagination query parameters.
/// </summary>
public class ComplaintListQueryValidator : AbstractValidator<ComplaintListQuery>
{
    private static readonly string[] AllowedSortBy = { "createdat", "updatedat" };
    private static readonly string[] AllowedSortDirection = { "asc", "desc" };

    public ComplaintListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid complaint status filter.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid complaint category filter.")
            .When(x => x.Category.HasValue);

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrEmpty(sortBy) || AllowedSortBy.Contains(sortBy.ToLowerInvariant()))
            .WithMessage("SortBy must be 'createdAt' or 'updatedAt'.");

        RuleFor(x => x.SortDirection)
            .Must(dir => string.IsNullOrEmpty(dir) || AllowedSortDirection.Contains(dir.ToLowerInvariant()))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}
