using FundTrack.Application.Dtos.Security;
using FluentValidation;

namespace FundTrack.Application.Validators.Security;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("CurrentPassword is required.");

        RuleFor(x => x.NewPassword).SetValidator(new PasswordValidator());

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must be different from the current one.");
    }
}
