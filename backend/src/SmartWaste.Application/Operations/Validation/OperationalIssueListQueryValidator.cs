using FluentValidation;
using SmartWaste.Application.Operations.Queries;

namespace SmartWaste.Application.Operations.Validation;

/// <summary>
/// Validator for operational issue listing and pagination query parameters.
/// </summary>
public class OperationalIssueListQueryValidator : AbstractValidator<OperationalIssueListQuery>
{
    private static readonly string[] AllowedSortBy = { "createdat", "updatedat" };
    private static readonly string[] AllowedSortDirection = { "asc", "desc" };

    public OperationalIssueListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid operational issue status filter.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.IssueType)
            .IsInEnum().WithMessage("Invalid operational issue type filter.")
            .When(x => x.IssueType.HasValue);

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrEmpty(sortBy) || AllowedSortBy.Contains(sortBy.ToLowerInvariant()))
            .WithMessage("SortBy must be 'createdAt' or 'updatedAt'.");

        RuleFor(x => x.SortDirection)
            .Must(dir => string.IsNullOrEmpty(dir) || AllowedSortDirection.Contains(dir.ToLowerInvariant()))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}
