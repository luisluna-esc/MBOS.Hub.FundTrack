using FundTrack.Application.Dtos.Organization;
using FundTrack.core.Constants;
using FluentValidation;

namespace FundTrack.Application.Validators.Organization;

public class ChurchCloseRequestValidator : AbstractValidator<ChurchCloseRequestDto>
{
    public ChurchCloseRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is ChurchStatuses.Closed or ChurchStatuses.Merged)
            .WithMessage($"Status must be '{ChurchStatuses.Closed}' or '{ChurchStatuses.Merged}'.");

        RuleFor(x => x.ClosedAt)
            .NotEqual(default(DateOnly)).WithMessage("ClosedAt is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");

        RuleFor(x => x.MergedIntoChurchId)
            .NotNull().When(x => x.Status == ChurchStatuses.Merged)
            .WithMessage("MergedIntoChurchId is required when the church is merged.");
    }
}
