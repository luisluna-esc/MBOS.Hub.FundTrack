using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Services;
using FluentValidation;

namespace FundTrack.Application.Validators.Security;

public class AppUserValidator : AbstractValidator<AppUserDto>
{
    public AppUserValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        // Vacío al crear: el servicio lo arma con el nombre completo (nombre.apellido).
        RuleFor(x => x.Username)
            .Matches(Usernames.Pattern).When(x => !string.IsNullOrWhiteSpace(x.Username))
            .WithMessage("El usuario debe tener la forma nombre.apellido: solo letras minúsculas sin tildes, un punto y, si hace falta, un número al final.")
            .MaximumLength(100).WithMessage("El usuario no puede pasar de 100 caracteres.");

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
