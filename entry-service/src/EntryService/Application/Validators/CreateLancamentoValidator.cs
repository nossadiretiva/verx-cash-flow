using EntryService.Application.Commands;
using FluentValidation;

namespace EntryService.Application.Validators;

public sealed class CreateLancamentoValidator : AbstractValidator<CreateLancamentoCommand>
{
    private static readonly string[] TiposValidos = ["CREDITO", "DEBITO"];

    public CreateLancamentoValidator()
    {
        RuleFor(x => x.Tipo)
            .NotEmpty()
            .Must(t => TiposValidos.Contains(t?.ToUpperInvariant()))
            .WithMessage("Tipo deve ser CREDITO ou DEBITO.");

        RuleFor(x => x.Valor)
            .GreaterThan(0)
            .WithMessage("Valor deve ser maior que zero.");

        RuleFor(x => x.Data)
            .NotEmpty()
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("Data não pode ser futura.");
    }
}
