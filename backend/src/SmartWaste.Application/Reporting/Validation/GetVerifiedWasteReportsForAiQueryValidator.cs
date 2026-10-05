using FluentValidation;
using SmartWaste.Application.Reporting.Queries;

namespace SmartWaste.Application.Reporting.Validation;

/// <summary>
/// Validates bounded query parameters for the internal AI verified waste reports tool.
/// Enforces Page >= 1 and PageSize between 1 and 50.
/// </summary>
public class GetVerifiedWasteReportsForAiQueryValidator : AbstractValidator<GetVerifiedWasteReportsForAiQuery>
{
    public GetVerifiedWasteReportsForAiQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("PageSize must be between 1 and 50.");
    }
}
