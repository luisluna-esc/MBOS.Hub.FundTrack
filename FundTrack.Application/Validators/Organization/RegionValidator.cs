using FundTrack.Application.Dtos.Organization;
using FluentValidation;

namespace FundTrack.Application.Validators.Organization;

public class RegionValidator : AbstractValidator<RegionDto>
{
    public RegionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}
