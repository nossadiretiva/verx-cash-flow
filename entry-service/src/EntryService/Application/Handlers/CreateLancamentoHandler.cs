using System.Text.Json;
using EntryService.Application.Commands;
using EntryService.Domain.Entities;
using EntryService.Domain.ValueObjects;
using EntryService.Infrastructure.Data;
using EntryService.Infrastructure.Data.Entities;
using MediatR;

namespace EntryService.Application.Handlers;

public sealed class CreateLancamentoHandler(AppDbContext db, ILogger<CreateLancamentoHandler> logger)
    : IRequestHandler<CreateLancamentoCommand, CreateLancamentoResult>
{
    public async Task<CreateLancamentoResult> Handle(CreateLancamentoCommand request, CancellationToken ct)
    {
        var tipo = Enum.Parse<TipoLancamento>(request.Tipo, ignoreCase: true);
        var lancamento = Lancamento.Criar(tipo, request.Valor, request.Data, request.Descricao);

        var lancamentoEntity = new LancamentoEntity
        {
            Id = lancamento.Id,
            Tipo = lancamento.Tipo.ToString().ToUpperInvariant(),
            Valor = lancamento.Valor,
            Descricao = lancamento.Descricao,
            Data = lancamento.Data,
            CreatedAt = lancamento.CreatedAt
        };

        var payload = JsonSerializer.Serialize(new
        {
            event_id = Guid.NewGuid(),
            event_type = "LancamentoCriado",
            occurred_at = lancamento.CreatedAt,
            data = new
            {
                lancamento_id = lancamento.Id,
                tipo = lancamentoEntity.Tipo,
                valor = lancamento.Valor,
                data = lancamento.Data.ToString("yyyy-MM-dd")
            }
        });

        var outboxEvent = new OutboxEventEntity
        {
            Id = Guid.NewGuid(),
            EventType = "LancamentoCriado",
            Payload = payload,
            Status = "PENDING",
            LancamentoId = lancamento.Id,
            CreatedAt = DateTime.UtcNow
        };

        // Transação ACID: INSERT lancamento + INSERT outbox_event em um único COMMIT
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Lancamentos.Add(lancamentoEntity);
        db.OutboxEvents.Add(outboxEvent);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Lançamento {Id} registrado. Tipo={Tipo} Valor={Valor} Data={Data}",
            lancamento.Id, lancamentoEntity.Tipo, lancamento.Valor, lancamento.Data);

        return new CreateLancamentoResult(lancamento.Id, lancamento.CreatedAt);
    }
}
