using FluentValidation;

namespace FundTrack.Application.Validators.Security;

/// <summary>Reglas de contraseña compartidas por alta de usuario, recuperación y cambio de contraseña.</summary>
public class PasswordValidator : AbstractValidator<string>
{
    public PasswordValidator()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters.");
    }
}
