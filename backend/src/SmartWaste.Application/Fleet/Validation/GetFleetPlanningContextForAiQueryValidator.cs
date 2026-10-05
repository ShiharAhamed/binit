using FluentValidation;
using SmartWaste.Application.Fleet.Queries;

namespace SmartWaste.Application.Fleet.Validation;

/// <summary>
/// Validates bounded query parameters for the internal AI fleet planning context tool.
/// </summary>
public class GetFleetPlanningContextForAiQueryValidator : AbstractValidator<GetFleetPlanningContextForAiQuery>
{
    public GetFleetPlanningContextForAiQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("PageSize must be between 1 and 50.");
    }
}
