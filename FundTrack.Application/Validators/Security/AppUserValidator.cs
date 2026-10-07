using FundTrack.Application.Dtos.Security;
using FluentValidation;

namespace FundTrack.Application.Validators.Security;

public class AppUserValidator : AbstractValidator<AppUserDto>
{
    public AppUserValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");

        RuleFor(x => x.Ci)
            .MaximumLength(20).WithMessage("Ci must not exceed 20 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone must not exceed 30 characters.");

        When(x => !string.IsNullOrWhiteSpace(x.Password), () =>
        {
            RuleFor(x => x.Password!).SetValidator(new PasswordValidator());
        });
    }
}
