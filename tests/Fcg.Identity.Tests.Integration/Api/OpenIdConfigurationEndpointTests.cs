using System.Net;
using System.Text.Json;
using Fcg.Identity.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Fcg.Identity.Tests.Integration.Api;

[Collection("Integration")]
public class OpenIdConfigurationEndpointTests : IAsyncLifetime
{
    private const string Caminho = "/.well-known/openid-configuration";
    private const string BasePublica = "https://api.exemplo.test";

    private static readonly string[] _camposEsperados =
    [
        "issuer",
        "jwks_uri",
        "response_types_supported",
        "subject_types_supported",
        "id_token_signing_alg_values_supported",
    ];

    private readonly IdentityApiFactory _factory;
    private readonly HttpClient _client;

    // A base pública varia por host derivado com configuração in-memory, que é mesclada depois das
    // variáveis de ambiente. Semear Jwt__PublicBaseUrl por variável de ambiente contaminaria as
    // demais classes de teste, que rodam no mesmo processo.
    private readonly List<WebApplicationFactory<Program>> _derivadas = [];

    public OpenIdConfigurationEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (WebApplicationFactory<Program> derivada in _derivadas)
        {
            await derivada.DisposeAsync();
        }
    }

    [Fact]
    public async Task DeveRetornar200SemAutenticacao()
    {
        HttpResponseMessage resposta = await _client.GetAsync(Caminho);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeveDerivarJwksUriDoHostDaRequestQuandoBasePublicaNaoConfigurada()
    {
        JsonElement documento = await ObterDocumentoAsync(_client);

        string autoridade = _client.BaseAddress!.GetLeftPart(UriPartial.Authority);
        documento
            .GetProperty("jwks_uri")
            .GetString()
            .Should()
            .Be($"{autoridade}/.well-known/jwks.json");
    }

    [Fact]
    public async Task DeveUsarBasePublicaConfiguradaNoJwksUri()
    {
        using HttpClient cliente = ClienteComBasePublica(BasePublica);

        JsonElement documento = await ObterDocumentoAsync(cliente);

        documento
            .GetProperty("jwks_uri")
            .GetString()
            .Should()
            .Be($"{BasePublica}/.well-known/jwks.json");
    }

    [Fact]
    public async Task DeveIgnorarBarraFinalDaBasePublica()
    {
        using HttpClient cliente = ClienteComBasePublica($"{BasePublica}/");

        JsonElement documento = await ObterDocumentoAsync(cliente);

        documento
            .GetProperty("jwks_uri")
            .GetString()
            .Should()
            .Be($"{BasePublica}/.well-known/jwks.json");
    }

    [Fact]
    public async Task DeveAnunciarIssuerConfigurado()
    {
        JsonElement documento = await ObterDocumentoAsync(_client);

        documento.GetProperty("issuer").GetString().Should().Be(IdentityApiFactory.TestIssuer);
    }

    [Fact]
    public async Task DeveAnunciarJwksUriQueRespondeComAsChaves()
    {
        JsonElement documento = await ObterDocumentoAsync(_client);
        string jwksUri = documento.GetProperty("jwks_uri").GetString()!;

        HttpResponseMessage resposta = await _client.GetAsync(new Uri(jwksUri));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using var jwks = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        jwks.RootElement.GetProperty("keys").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeveExporOsCamposEsperadosDoDocumento()
    {
        JsonElement documento = await ObterDocumentoAsync(_client);

        foreach (string campo in _camposEsperados)
        {
            documento
                .TryGetProperty(campo, out _)
                .Should()
                .BeTrue($"o documento de discovery precisa expor o campo '{campo}'");
        }

        documento
            .GetProperty("id_token_signing_alg_values_supported")
            .EnumerateArray()
            .Select(algoritmo => algoritmo.GetString())
            .Should()
            .Contain("RS256");
    }

    private static async Task<JsonElement> ObterDocumentoAsync(HttpClient cliente)
    {
        string corpo = await cliente.GetStringAsync(Caminho);
        using var documento = JsonDocument.Parse(corpo);
        return documento.RootElement.Clone();
    }

    private HttpClient ClienteComBasePublica(string valor)
    {
        WebApplicationFactory<Program> derivada = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?> { ["Jwt:PublicBaseUrl"] = valor }
                    )
            )
        );

        _derivadas.Add(derivada);

        return derivada.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }
}
