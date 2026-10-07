using FundTrack.Application.Dtos.Organization;
using FluentValidation;

namespace FundTrack.Application.Validators.Organization;

public class DistrictPastorAssignRequestValidator : AbstractValidator<DistrictPastorAssignRequestDto>
{
    public DistrictPastorAssignRequestValidator()
    {
        RuleFor(x => x.PastorId)
            .GreaterThan(0).WithMessage("PastorId is required.");

        RuleFor(x => x.StartDate)
            .NotEqual(default(DateOnly)).WithMessage("StartDate is required.");
    }
}
