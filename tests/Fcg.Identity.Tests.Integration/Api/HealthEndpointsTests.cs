using System.Net;
using System.Text.Json;
using Fcg.Identity.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fcg.Identity.Tests.Integration.Api;

public class HealthEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    [Fact]
    public async Task LivenessDeveRetornar200()
    {
        HttpResponseMessage resposta = await _client.GetAsync("/health/live");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessDeveRetornar200ComSqlServerAtivo()
    {
        HttpResponseMessage resposta = await _client.GetAsync("/health/ready");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessSoDeveConterCheckDeSqlServer()
    {
        HttpResponseMessage resposta = await _client.GetAsync("/health/ready");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        string json = await resposta.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        JsonElement entries = doc.RootElement.GetProperty("entries");

        // Apenas o check de SQL Server (IdentityDbContext) deve ser avaliado no /health/ready
        int contagem = entries.EnumerateObject().Count();
        contagem.Should().Be(1);
        entries.TryGetProperty("IdentityDbContext", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ReadinessDeveContinuar200ComBrokerForaDoAr()
    {
        // Estado saudável com o broker de pé.
        HttpResponseMessage antes = await _client.GetAsync("/health/ready");
        antes.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.PararBrokerAsync();

        // Com o broker derrubado, a readiness não pode regredir: o Outbox desacopla a API do RabbitMQ,
        // então só o SQL Server entra no /health/ready.
        HttpResponseMessage depois = await _client.GetAsync("/health/ready");
        depois.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
