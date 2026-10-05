using FluentValidation;
using SmartWaste.Application.Collection.Queries;

namespace SmartWaste.Application.Collection.Validation;

/// <summary>
/// Validator for bin observation history pagination queries.
/// </summary>
public class ObservationListQueryValidator : AbstractValidator<ObservationListQuery>
{
    public ObservationListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}
