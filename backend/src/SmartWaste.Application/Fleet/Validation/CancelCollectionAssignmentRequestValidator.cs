using FluentValidation;
using SmartWaste.Application.Collection.DTOs.Requests;

namespace SmartWaste.Application.Collection.Validation;

public sealed class CancelCollectionAssignmentRequestValidator : AbstractValidator<CancelCollectionAssignmentRequest>
{
    public CancelCollectionAssignmentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().Length(5, 500);
    }
}
