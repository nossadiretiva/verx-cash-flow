namespace EntryService.Infrastructure.Data.Entities;

public sealed class LancamentoEntity
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = default!;
    public decimal Valor { get; set; }
    public string? Descricao { get; set; }
    public DateOnly Data { get; set; }
    public DateTime CreatedAt { get; set; }
}
