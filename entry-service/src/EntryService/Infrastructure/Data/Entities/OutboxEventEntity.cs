namespace EntryService.Infrastructure.Data.Entities;

public sealed class OutboxEventEntity
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public string Status { get; set; } = "PENDING";
    public Guid LancamentoId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}
