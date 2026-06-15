<<<<<<< HEAD
using System.Diagnostics.Metrics;
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
using Amazon.SQS;
using EntryService.Application.Commands;
using EntryService.Application.Validators;
using EntryService.Infrastructure.Data;
using EntryService.Infrastructure.Messaging;
<<<<<<< HEAD
using EntryService.Infrastructure.Metrics;
using EntryService.Infrastructure.Outbox;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
=======
using EntryService.Infrastructure.Outbox;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
<<<<<<< HEAD
    .Enrich.WithSpan()
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
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

<<<<<<< HEAD
// ── Métricas customizadas ─────────────────────────────────────────────────────
builder.Services.AddSingleton<LancamentosMetrics>();

// ── OpenTelemetry (Tracing + Metrics) ─────────────────────────────────────────
var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];
var otelBuilder = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("entry-service"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
         .AddEntityFrameworkCoreInstrumentation()
         .AddSource(OutboxWorker.ActivitySourceName);

        if (!string.IsNullOrEmpty(otlpEndpoint))
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
         .AddRuntimeInstrumentation()
         .AddMeter(LancamentosMetrics.MeterName)
         .AddPrometheusExporter();
    });

// ── JWT Auth ──────────────────────────────────────────────────────────────────
// MetadataAddress aponta para o Keycloak via DNS interno (Docker).
// ValidIssuers aceita tanto o issuer interno quanto o externo (localhost),
// pois tokens obtidos fora do Docker terão iss=http://localhost:8180/...
var internalIssuer = builder.Configuration["Auth:Issuer"]!;
var externalIssuer = builder.Configuration["Auth:ExternalIssuer"] ?? internalIssuer;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress = $"{internalIssuer}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuers    = [internalIssuer, externalIssuer],
            ValidAudience   = builder.Configuration["Auth:Audience"],
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Keycloak emite scope como string única separada por espaço ("cashflow:write cashflow:read")
    options.AddPolicy("write", policy =>
        policy.RequireAssertion(ctx =>
            (ctx.User.FindFirst("scope")?.Value ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("cashflow:write")));
});
=======
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
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2

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

<<<<<<< HEAD
app.UseAuthentication();
app.UseAuthorization();

=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapPost("/lancamentos", async (
    CreateLancamentoRequest req,
    IValidator<CreateLancamentoCommand> validator,
    IMediator mediator,
<<<<<<< HEAD
    LancamentosMetrics metrics,
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
    CancellationToken ct) =>
{
    var command = new CreateLancamentoCommand(req.Tipo, req.Valor, req.Data, req.Descricao);

    var validation = await validator.ValidateAsync(command, ct);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var result = await mediator.Send(command, ct);
<<<<<<< HEAD

    metrics.LancamentoCriado(req.Tipo);

    return Results.Created($"/lancamentos/{result.Id}", new { result.Id, result.Timestamp });
}).RequireAuthorization("write");

app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");
=======
    return Results.Created($"/lancamentos/{result.Id}", new { result.Id, result.Timestamp });
});

app.MapHealthChecks("/health");
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2

app.Run();

// ── DTOs ──────────────────────────────────────────────────────────────────────
public sealed record CreateLancamentoRequest(
    string Tipo,
    decimal Valor,
    DateOnly Data,
    string? Descricao);

public partial class Program { }
