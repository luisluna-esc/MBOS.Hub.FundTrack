using FundTrack.Application.Dtos.Organization;
using FluentValidation;

namespace FundTrack.Application.Validators.Organization;

public class PastorRequestValidator : AbstractValidator<PastorRequestDto>
{
    public PastorRequestValidator()
    {
        RuleFor(x => x.Ci)
            .MaximumLength(20).WithMessage("Ci must not exceed 20 characters.");

        RuleFor(x => x.FirstNames)
            .NotEmpty().WithMessage("FirstNames is required.")
            .MaximumLength(100).WithMessage("FirstNames must not exceed 100 characters.");

        RuleFor(x => x.LastNames)
            .NotEmpty().WithMessage("LastNames is required.")
            .MaximumLength(100).WithMessage("LastNames must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Email must be a valid email address.")
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");
    }
}
