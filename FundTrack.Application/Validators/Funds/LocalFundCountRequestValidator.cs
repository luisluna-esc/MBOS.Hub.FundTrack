using FundTrack.Application.Dtos.Funds;
using FundTrack.core.Constants;
using FluentValidation;

namespace FundTrack.Application.Validators.Funds;

public class LocalFundCountRequestValidator : AbstractValidator<LocalFundCountRequestDto>
{
    public LocalFundCountRequestValidator()
    {
        RuleFor(x => x.ChurchId)
            .GreaterThan(0).WithMessage("ChurchId is required.");

        RuleFor(x => x.PeriodId)
            .GreaterThan(0).WithMessage("PeriodId is required.");

        RuleFor(x => x.SubmissionStatus)
            .Must(status => SubmissionStatuses.All.Contains(status))
            .WithMessage($"SubmissionStatus must be one of: {string.Join(", ", SubmissionStatuses.All)}.");

        // El saldo ACMS sí puede ser negativo (pasa cuando la iglesia gasta más de lo que tiene),
        // pero lo que se cuenta en efectivo y en banco no.
        When(x => x.SubmissionStatus == SubmissionStatuses.Submitted, () =>
        {
            RuleFor(x => x.CashAmount)
                .GreaterThanOrEqualTo(0).WithMessage("CashAmount must not be negative.");

            RuleFor(x => x.BankAmount)
                .GreaterThanOrEqualTo(0).WithMessage("BankAmount must not be negative.");

            RuleFor(x => x.CountDate)
                .NotNull().WithMessage("CountDate is required when the count was submitted.");
        });

        RuleFor(x => x.TreasurerName).MaximumLength(150).WithMessage("TreasurerName must not exceed 150 characters.");
        RuleFor(x => x.ElderName).MaximumLength(150).WithMessage("ElderName must not exceed 150 characters.");
        RuleFor(x => x.Observation).MaximumLength(1000).WithMessage("Observation must not exceed 1000 characters.");
    }
}
