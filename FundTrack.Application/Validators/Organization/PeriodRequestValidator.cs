using FundTrack.Application.Dtos.Organization;
using FluentValidation;

namespace FundTrack.Application.Validators.Organization;

public class PeriodRequestValidator : AbstractValidator<PeriodRequestDto>
{
    public PeriodRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");
    }
}
