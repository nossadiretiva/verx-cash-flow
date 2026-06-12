using EntryService.Application.Commands;
using EntryService.Application.Validators;
using FluentAssertions;

namespace EntryService.Unit.Tests.Application;

public sealed class CreateLancamentoValidatorTests
{
    private readonly CreateLancamentoValidator _validator = new();

    [Theory]
    [InlineData("CREDITO")]
    [InlineData("DEBITO")]
    [InlineData("credito")]
    [InlineData("debito")]
    public async Task Validate_TipoValido_Sucesso(string tipo)
    {
        var command = new CreateLancamentoCommand(tipo, 100m, DateOnly.FromDateTime(DateTime.Today), null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("TRANSFERENCIA")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Validate_TipoInvalido_Falha(string tipo)
    {
        var command = new CreateLancamentoCommand(tipo, 100m, DateOnly.FromDateTime(DateTime.Today), null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Tipo");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_ValorInvalido_Falha(decimal valor)
    {
        var command = new CreateLancamentoCommand("CREDITO", valor, DateOnly.FromDateTime(DateTime.Today), null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Valor");
    }

    [Fact]
    public async Task Validate_DataFutura_Falha()
    {
        var amanha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var command = new CreateLancamentoCommand("CREDITO", 100m, amanha, null);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Data");
    }
}
