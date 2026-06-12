using MediatR;

namespace EntryService.Application.Commands;

public sealed record CreateLancamentoCommand(
    string Tipo,
    decimal Valor,
    DateOnly Data,
    string? Descricao
) : IRequest<CreateLancamentoResult>;

public sealed record CreateLancamentoResult(Guid Id, DateTime Timestamp);
