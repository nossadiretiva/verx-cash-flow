<<<<<<< HEAD
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
=======
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
using Testcontainers.PostgreSql;
using EntryService.Infrastructure.Data;

namespace EntryService.Integration.Tests;

public sealed class LancamentosEndpointTests : IAsyncLifetime
{
<<<<<<< HEAD
    // Chave simétrica usada exclusivamente nos testes para assinar tokens fake
    private const string TestSigningKey = "test-signing-key-for-integration-tests-only!";
    private const string TestIssuer = "test-issuer";
    private const string TestAudience = "cashflow-api";

=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
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
<<<<<<< HEAD

                    // Substitui validação JWT por chave simétrica de teste (sem Keycloak)
                    services.PostConfigure<JwtBearerOptions>(
                        JwtBearerDefaults.AuthenticationScheme, options =>
                        {
                            options.Authority = null;
                            options.MetadataAddress = null;
                            options.RequireHttpsMetadata = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey = new SymmetricSecurityKey(
                                    Encoding.UTF8.GetBytes(TestSigningKey)),
                                ValidIssuer = TestIssuer,
                                ValidAudience = TestAudience,
                                ValidateLifetime = false,
                            };
                        });
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
                });
            });

        _client = _factory.CreateClient();

<<<<<<< HEAD
=======
        // Aplica migrations
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        _factory.Dispose();
    }

<<<<<<< HEAD
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GenerateToken(string scope = "cashflow:write")
    {
        var claims = new[]
        {
            new Claim("scope", scope),
            new Claim(JwtRegisteredClaimNames.Sub, "service-account-test"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void SetAuthHeader(string scope = "cashflow:write") =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateToken(scope));

    // ── Testes existentes (agora com token) ───────────────────────────────────

    [Fact]
    public async Task PostLancamentos_DadosValidos_Retorna201()
    {
        SetAuthHeader();
=======
    [Fact]
    public async Task PostLancamentos_DadosValidos_Retorna201()
    {
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
        var payload = new
        {
            tipo = "CREDITO",
            valor = 150.00m,
            data = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            descricao = "Venda à vista"
        };

        var response = await _client.PostAsJsonAsync("/lancamentos", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
<<<<<<< HEAD
=======

>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostLancamentos_ValorZero_Retorna400()
    {
<<<<<<< HEAD
        SetAuthHeader();
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
        var payload = new { tipo = "CREDITO", valor = 0m, data = "2024-01-15" };

        var response = await _client.PostAsJsonAsync("/lancamentos", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostLancamentos_TipoInvalido_Retorna400()
    {
<<<<<<< HEAD
        SetAuthHeader();
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
        var payload = new { tipo = "TRANSFERENCIA", valor = 100m, data = "2024-01-15" };

        var response = await _client.PostAsJsonAsync("/lancamentos", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostLancamentos_Persiste_LancamentoEOutboxEvent()
    {
<<<<<<< HEAD
        SetAuthHeader();
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
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
<<<<<<< HEAD

    // ── Testes de autenticação (Sprint 5) ─────────────────────────────────────

    [Fact]
    public async Task PostLancamentos_SemToken_Retorna401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/lancamentos",
            new { tipo = "CREDITO", valor = 100m, data = "2024-01-15" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostLancamentos_TokenSemScopeWrite_Retorna403()
    {
        SetAuthHeader(scope: "cashflow:read");

        var response = await _client.PostAsJsonAsync("/lancamentos",
            new { tipo = "CREDITO", valor = 100m, data = "2024-01-15" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostLancamentos_TokenScopeWrite_Retorna201()
    {
        SetAuthHeader(scope: "cashflow:write");
        var payload = new
        {
            tipo = "CREDITO",
            valor = 200m,
            data = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
        };

        var response = await _client.PostAsJsonAsync("/lancamentos", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostLancamentos_TokenInvalido_Retorna401()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "token.invalido.aqui");

        var response = await _client.PostAsJsonAsync("/lancamentos",
            new { tipo = "CREDITO", valor = 100m, data = "2024-01-15" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
}
