using FundTrack.Application.Dtos.Funds;
using FundTrack.core.Constants;
using FluentValidation;

namespace FundTrack.Application.Validators.Funds;

public class DifferenceFollowUpRequestValidator : AbstractValidator<DifferenceFollowUpRequestDto>
{
    public DifferenceFollowUpRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .Must(status => status is DifferenceStatuses.Justified or DifferenceStatuses.Resolved)
            .WithMessage($"NewStatus must be '{DifferenceStatuses.Justified}' or '{DifferenceStatuses.Resolved}'.");

        RuleFor(x => x.Justification)
            .NotEmpty().WithMessage("Justification is required.")
            .MaximumLength(1000).WithMessage("Justification must not exceed 1000 characters.");
    }
}
