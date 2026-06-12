using System.Diagnostics.Metrics;

namespace EntryService.Infrastructure.Metrics;

public sealed class LancamentosMetrics : IDisposable
{
    public const string MeterName = "EntryService";

    private readonly Meter _meter;
    private readonly Counter<long> _lancamentosTotal;
    private readonly Counter<long> _outboxPublicadosTotal;
    private readonly Counter<long> _outboxFalhasTotal;

    public LancamentosMetrics()
    {
        _meter = new Meter(MeterName);

        _lancamentosTotal = _meter.CreateCounter<long>(
            "lancamentos_criados_total",
            description: "Total de lançamentos criados, particionado por tipo");

        _outboxPublicadosTotal = _meter.CreateCounter<long>(
            "outbox_eventos_publicados_total",
            description: "Total de eventos publicados com sucesso no SQS via Outbox");

        _outboxFalhasTotal = _meter.CreateCounter<long>(
            "outbox_eventos_falhas_total",
            description: "Total de falhas ao publicar eventos no SQS");
    }

    public void LancamentoCriado(string tipo) =>
        _lancamentosTotal.Add(1, new KeyValuePair<string, object?>("tipo", tipo.ToUpperInvariant()));

    public void OutboxEventoPublicado() =>
        _outboxPublicadosTotal.Add(1);

    public void OutboxEventoFalhou() =>
        _outboxFalhasTotal.Add(1);

    public void Dispose() => _meter.Dispose();
}
