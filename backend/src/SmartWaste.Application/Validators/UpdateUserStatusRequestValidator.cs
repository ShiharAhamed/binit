using FluentValidation;
using SmartWaste.Application.DTOs.Users;

namespace SmartWaste.Application.Validators;

public class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x).NotNull();
    }
}
