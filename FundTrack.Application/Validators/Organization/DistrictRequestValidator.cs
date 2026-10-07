using FundTrack.Application.Dtos.Organization;
using FluentValidation;

namespace FundTrack.Application.Validators.Organization;

public class DistrictRequestValidator : AbstractValidator<DistrictRequestDto>
{
    public DistrictRequestValidator()
    {
        RuleFor(x => x.RegionId)
            .GreaterThan(0).WithMessage("RegionId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}
