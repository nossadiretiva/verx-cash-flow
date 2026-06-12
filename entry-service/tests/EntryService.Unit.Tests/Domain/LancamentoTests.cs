using EntryService.Domain.Entities;
using EntryService.Domain.ValueObjects;
using FluentAssertions;

namespace EntryService.Unit.Tests.Domain;

public sealed class LancamentoTests
{
    [Fact]
    public void Criar_ComDadosValidos_RetornaLancamento()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 100m, DateOnly.FromDateTime(DateTime.Today));

        lancamento.Id.Should().NotBeEmpty();
        lancamento.Tipo.Should().Be(TipoLancamento.Credito);
        lancamento.Valor.Should().Be(100m);
        lancamento.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void Criar_ComValorInvalido_LancaExcecao(decimal valor)
    {
        var act = () => Lancamento.Criar(TipoLancamento.Debito, valor, DateOnly.FromDateTime(DateTime.Today));

        act.Should().Throw<ArgumentException>().WithMessage("*Valor deve ser maior que zero*");
    }

    [Fact]
    public void Criar_ComDescricao_PreencheDescricao()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Debito, 50m, DateOnly.FromDateTime(DateTime.Today), "Aluguel");

        lancamento.Descricao.Should().Be("Aluguel");
    }

    [Fact]
    public void Criar_SemDescricao_DescricaoNula()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 200m, DateOnly.FromDateTime(DateTime.Today));

        lancamento.Descricao.Should().BeNull();
    }
}
