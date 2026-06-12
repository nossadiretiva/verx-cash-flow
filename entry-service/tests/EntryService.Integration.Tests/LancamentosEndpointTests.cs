using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using EntryService.Infrastructure.Data;

namespace EntryService.Integration.Tests;

public sealed class LancamentosEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("cashflow_test")
        .WithUsername("cashflow")
        .WithPassword("cashflow")
        .Build();

    private WebApplicationFactory<Program> _factory = default!;
    private HttpClient _client = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Substitui PostgreSQL real pelo Testcontainer
                    var descriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(opt =>
                        opt.UseNpgsql(_postgres.GetConnectionString()));
                });
            });

        _client = _factory.CreateClient();

        // Aplica migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        _factory.Dispose();
    }

    [Fact]
    public async Task PostLancamentos_DadosValidos_Retorna201()
    {
        var payload = new
        {
            tipo = "CREDITO",
            valor = 150.00m,
            data = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            descricao = "Venda à vista"
        };

        var response = await _client.PostAsJsonAsync("/lancamentos", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostLancamentos_ValorZero_Retorna400()
    {
        var payload = new { tipo = "CREDITO", valor = 0m, data = "2024-01-15" };

        var response = await _client.PostAsJsonAsync("/lancamentos", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostLancamentos_TipoInvalido_Retorna400()
    {
        var payload = new { tipo = "TRANSFERENCIA", valor = 100m, data = "2024-01-15" };

        var response = await _client.PostAsJsonAsync("/lancamentos", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostLancamentos_Persiste_LancamentoEOutboxEvent()
    {
        var payload = new { tipo = "DEBITO", valor = 75.50m, data = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd") };

        await _client.PostAsJsonAsync("/lancamentos", payload);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lancamentos = await db.Lancamentos.ToListAsync();
        lancamentos.Should().HaveCount(1);
        lancamentos[0].Tipo.Should().Be("DEBITO");
        lancamentos[0].Valor.Should().Be(75.50m);

        var outboxEvents = await db.OutboxEvents.ToListAsync();
        outboxEvents.Should().HaveCount(1);
        outboxEvents[0].Status.Should().Be("PENDING");
        outboxEvents[0].EventType.Should().Be("LancamentoCriado");
    }
}
