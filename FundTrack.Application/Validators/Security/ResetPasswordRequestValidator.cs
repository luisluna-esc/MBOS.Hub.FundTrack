using FundTrack.Application.Dtos.Security;
using FluentValidation;

namespace FundTrack.Application.Validators.Security;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required.");

        RuleFor(x => x.NewPassword).SetValidator(new PasswordValidator());
    }
}
