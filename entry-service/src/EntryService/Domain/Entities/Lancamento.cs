using EntryService.Domain.ValueObjects;

namespace EntryService.Domain.Entities;

public sealed class Lancamento
{
    public Guid Id { get; private set; }
    public TipoLancamento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public string? Descricao { get; private set; }
    public DateOnly Data { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Lancamento() { }

    public static Lancamento Criar(TipoLancamento tipo, decimal valor, DateOnly data, string? descricao = null)
    {
        if (valor <= 0)
            throw new ArgumentException("Valor deve ser maior que zero.", nameof(valor));

        return new Lancamento
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Valor = valor,
            Data = data,
            Descricao = descricao,
            CreatedAt = DateTime.UtcNow
        };
    }
}
