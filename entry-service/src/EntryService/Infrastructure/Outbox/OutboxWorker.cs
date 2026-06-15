using System.Diagnostics;
using EntryService.Infrastructure.Data;
using EntryService.Infrastructure.Messaging;
using EntryService.Infrastructure.Metrics;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace EntryService.Infrastructure.Outbox;

public sealed class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    public const string ActivitySourceName = "EntryService.OutboxWorker";

    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    private readonly AsyncRetryPolicy _retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            (ex, delay, attempt, _) =>
                logger.LogWarning(ex, "Retry {Attempt} ao publicar no SQS. Aguardando {Delay}s", attempt, delay.TotalSeconds));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxWorker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erro inesperado no OutboxWorker.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessPendingEventsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<ISqsPublisher>();
        var metrics = scope.ServiceProvider.GetRequiredService<LancamentosMetrics>();

        var pending = await db.OutboxEvents
            .Where(e => e.Status == "PENDING")
            .OrderBy(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        using var batchActivity = _activitySource.StartActivity("outbox.process_batch");
        batchActivity?.SetTag("outbox.batch_size", pending.Count);

        logger.LogInformation("OutboxWorker processando {Count} evento(s) pendente(s).", pending.Count);

        foreach (var evt in pending)
        {
            using var eventActivity = _activitySource.StartActivity("outbox.publish_event");
            eventActivity?.SetTag("outbox.event_id", evt.Id.ToString());
            eventActivity?.SetTag("outbox.event_type", evt.EventType);

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await publisher.PublishAsync(evt.Payload, ct);
                    evt.Status = "PUBLISHED";
                    evt.PublishedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                });

                metrics.OutboxEventoPublicado();
                eventActivity?.SetStatus(ActivityStatusCode.Ok);
                logger.LogInformation("Evento {EventId} publicado com sucesso.", evt.Id);
            }
            catch (Exception ex)
            {
                evt.Status = "FAILED";
                await db.SaveChangesAsync(ct);
                metrics.OutboxEventoFalhou();
                eventActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Falha definitiva ao publicar evento {EventId}.", evt.Id);
            }
        }
    }
}
