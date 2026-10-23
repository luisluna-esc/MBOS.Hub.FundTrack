using FundTrack.Application.Dtos.Funds;
using FundTrack.Application.Services;
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
        // pero lo que tiene el tesorero (banco, efectivo, dólares y documentos) no.
        // Arqueo hecho por la iglesia: solo el total, sin fecha obligatoria ni explicación de la diferencia.
        When(x => x.SubmissionStatus == SubmissionStatuses.Submitted && x.SummaryOnly, () =>
        {
            RuleFor(x => x.SummaryTotal).GreaterThanOrEqualTo(0).WithMessage("El total del arqueo no puede ser negativo.");
            RuleFor(x => x.CountDate).Must(NotInFuture).WithMessage("La fecha del arqueo no puede ser futura.");
        });

        When(x => x.SubmissionStatus == SubmissionStatuses.Submitted && !x.SummaryOnly, () =>
        {
            RuleFor(x => x.BankStatementBalance).GreaterThanOrEqualTo(0).WithMessage("El saldo según extracto bancario no puede ser negativo.");
            RuleFor(x => x.BankDeposit).GreaterThanOrEqualTo(0).WithMessage("El depósito bancario no puede ser negativo.");
            RuleFor(x => x.LocalCash).GreaterThanOrEqualTo(0).WithMessage("Los billetes y monedas no pueden ser negativos.");
            RuleFor(x => x.UsdAmount).GreaterThanOrEqualTo(0).WithMessage("Los dólares no pueden ser negativos.");

            RuleFor(x => x.ExchangeRate)
                .GreaterThan(0).When(x => x.UsdAmount > 0)
                .WithMessage("Indica el tipo de cambio de los dólares.");

            RuleFor(x => x.CountDate)
                .NotNull().WithMessage("La fecha del arqueo es obligatoria cuando la iglesia presentó su arqueo.")
                .Must(NotInFuture).WithMessage("La fecha del arqueo no puede ser futura.");

            RuleForEach(x => x.Documents).ChildRules(document =>
            {
                document.RuleFor(d => d.DocumentDate)
                    .NotEqual(default(DateOnly)).WithMessage("Cada documento necesita su fecha.")
                    .Must(date => NotInFuture(date)).WithMessage("La fecha de un documento no puede ser futura.");
                document.RuleFor(d => d.DocumentNumber)
                    .MaximumLength(50).WithMessage("El número del documento no puede pasar de 50 caracteres.");
                document.RuleFor(d => d.Detail)
                    .NotEmpty().WithMessage("Cada documento necesita su detalle.")
                    .MaximumLength(500).WithMessage("El detalle del documento no puede pasar de 500 caracteres.");
                document.RuleFor(d => d.Amount)
                    .GreaterThan(0).WithMessage("El monto de cada documento debe ser mayor a cero.");
            });

            // Si el total en custodia no cuadra con el ACMS hay que explicar por qué.
            RuleFor(x => x.Observation)
                .NotEmpty()
                .When(x => LocalFundCountService.TotalInCustody(x) != x.AcmsBalance)
                .WithMessage("Hay diferencia con el saldo ACMS: explica el motivo en la observación.");
        });

        RuleFor(x => x.TreasurerName).MaximumLength(150).WithMessage("TreasurerName must not exceed 150 characters.");
        RuleFor(x => x.ElderName).MaximumLength(150).WithMessage("ElderName must not exceed 150 characters.");
        RuleFor(x => x.Observation).MaximumLength(1000).WithMessage("Observation must not exceed 1000 characters.");
    }

    /// <summary>No se registran fechas futuras; "hoy" es el día en Bolivia, no en UTC.</summary>
    private static bool NotInFuture(DateOnly? date) =>
        date is null || date.Value <= DateOnly.FromDateTime(BoliviaTime.Today);
}
