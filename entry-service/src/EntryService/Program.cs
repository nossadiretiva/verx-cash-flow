using Amazon.SQS;
using EntryService.Application.Commands;
using EntryService.Application.Validators;
using EntryService.Infrastructure.Data;
using EntryService.Infrastructure.Messaging;
using EntryService.Infrastructure.Outbox;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreateLancamentoCommand>());

// ── FluentValidation ──────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<CreateLancamentoValidator>();

// ── EF Core + PostgreSQL ──────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── AWS SQS ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    var endpointUrl = builder.Configuration["Sqs:EndpointUrl"];
    var sqsConfig = new AmazonSQSConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 };
    if (!string.IsNullOrEmpty(endpointUrl))
        sqsConfig.ServiceURL = endpointUrl;
    return new AmazonSQSClient("test", "test", sqsConfig);
});
builder.Services.AddScoped<ISqsPublisher, SqsPublisher>();

// ── Outbox Worker ─────────────────────────────────────────────────────────────
builder.Services.AddHostedService<OutboxWorker>();

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];
if (!string.IsNullOrEmpty(otlpEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("entry-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));
}

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Postgres") ?? string.Empty,
        name: "postgres");

var app = builder.Build();

// ── Auto-migrations em desenvolvimento ───────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapPost("/lancamentos", async (
    CreateLancamentoRequest req,
    IValidator<CreateLancamentoCommand> validator,
    IMediator mediator,
    CancellationToken ct) =>
{
    var command = new CreateLancamentoCommand(req.Tipo, req.Valor, req.Data, req.Descricao);

    var validation = await validator.ValidateAsync(command, ct);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var result = await mediator.Send(command, ct);
    return Results.Created($"/lancamentos/{result.Id}", new { result.Id, result.Timestamp });
});

app.MapHealthChecks("/health");

app.Run();

// ── DTOs ──────────────────────────────────────────────────────────────────────
public sealed record CreateLancamentoRequest(
    string Tipo,
    decimal Valor,
    DateOnly Data,
    string? Descricao);

public partial class Program { }
