using FundTrack.Application.Dtos.Funds;
using FundTrack.core.Constants;
using FluentValidation;

namespace FundTrack.Application.Validators.Funds;

public class LocalFundCountCorrectionRequestValidator : AbstractValidator<LocalFundCountCorrectionRequestDto>
{
    public LocalFundCountCorrectionRequestValidator()
    {
        RuleFor(x => x.FieldName)
            .Must(field => CorrectionFields.All.Contains(field))
            .WithMessage($"FieldName must be one of: {string.Join(", ", CorrectionFields.All)}.");

        RuleFor(x => x.CorrectedAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.FieldName == CorrectionFields.TotalAmount)
            .WithMessage("CorrectedAmount must not be negative for the total in custody.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}
