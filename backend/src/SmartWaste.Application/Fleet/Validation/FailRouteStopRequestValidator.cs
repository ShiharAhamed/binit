using FluentValidation;
using SmartWaste.Application.Collection.DTOs.Requests;

namespace SmartWaste.Application.Collection.Validation;

public sealed class FailRouteStopRequestValidator : AbstractValidator<FailRouteStopRequest>
{
    public FailRouteStopRequestValidator() => RuleFor(x => x.Reason).NotEmpty().Length(5, 500);
}
