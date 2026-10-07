using FundTrack.Application.Dtos.Organization;
using FundTrack.core.Constants;
using FluentValidation;

namespace FundTrack.Application.Validators.Organization;

public class ChurchRequestValidator : AbstractValidator<ChurchRequestDto>
{
    public ChurchRequestValidator()
    {
        // La Filial toma el código de su madre, por eso solo se exige en Iglesias y Grupos.
        RuleFor(x => x.Code)
            .NotEmpty().When(x => x.Type != ChurchTypes.Branch).WithMessage("Code is required.")
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");

        RuleFor(x => x.ParentChurchId)
            .NotNull().When(x => x.Type == ChurchTypes.Branch)
            .WithMessage("ParentChurchId is required for a Filial: it is the Iglesia or Grupo it belongs to.");

        RuleFor(x => x.ParentChurchId)
            .Null().When(x => x.Type != ChurchTypes.Branch)
            .WithMessage("Only a Filial can have ParentChurchId.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.Type)
            .Must(type => ChurchTypes.All.Contains(type))
            .WithMessage($"Type must be one of: {string.Join(", ", ChurchTypes.All)}.");

        // La Filial toma el distrito de su madre.
        RuleFor(x => x.DistrictId)
            .GreaterThan(0).When(x => x.Type != ChurchTypes.Branch).WithMessage("DistrictId is required.");

        RuleFor(x => x.Address).MaximumLength(255).WithMessage("Address must not exceed 255 characters.");
        RuleFor(x => x.Phone).MaximumLength(30).WithMessage("Phone must not exceed 30 characters.");
        RuleFor(x => x.TreasurerName).MaximumLength(150).WithMessage("TreasurerName must not exceed 150 characters.");
        RuleFor(x => x.TreasurerPhone).MaximumLength(30).WithMessage("TreasurerPhone must not exceed 30 characters.");
        RuleFor(x => x.ElderName).MaximumLength(150).WithMessage("ElderName must not exceed 150 characters.");
        RuleFor(x => x.ElderPhone).MaximumLength(30).WithMessage("ElderPhone must not exceed 30 characters.");
        RuleFor(x => x.ChangeReason).MaximumLength(500).WithMessage("ChangeReason must not exceed 500 characters.");
    }
}
