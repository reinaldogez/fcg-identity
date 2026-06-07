using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fcg.Identity.Tests.Integration.Api;

[Collection("Integration")]
public class JwksEndpointTests : IAsyncLifetime
{
    private const string EmailUsuario = "jwks@fcg.com";
    private const string SenhaUsuario = "Senha@123";
    private const string NomeUsuario = "Usuario Jwks";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityApiFactory _factory;
    private readonly HttpClient _client;

    public JwksEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public Task InitializeAsync() => _factory.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeveRetornar200SemAutenticacao()
    {
        HttpResponseMessage resposta = await _client.GetAsync("/.well-known/jwks.json");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeveExporChavePublicaComCamposEsperados()
    {
        JsonElement jwk = await ObterPrimeiraChaveAsync();

        jwk.GetProperty("kid").GetString().Should().Be(IdentityApiFactory.TestKeyId);
        jwk.GetProperty("kty").GetString().Should().Be("RSA");
        jwk.GetProperty("alg").GetString().Should().Be("RS256");
        jwk.GetProperty("use").GetString().Should().Be("sig");
        jwk.GetProperty("n").GetString().Should().NotBeNullOrWhiteSpace();
        jwk.GetProperty("e").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NaoDeveExporNenhumCampoPrivadoDaChave()
    {
        JsonElement jwk = await ObterPrimeiraChaveAsync();

        foreach (string campoPrivado in new[] { "d", "p", "q", "dp", "dq", "qi" })
        {
            jwk.TryGetProperty(campoPrivado, out _)
                .Should()
                .BeFalse($"o JWKS não pode expor o campo privado '{campoPrivado}'");
        }
    }

    [Fact]
    public async Task DeveUsarMesmoKidDoHeaderDoTokenEmitido()
    {
        JsonElement jwk = await ObterPrimeiraChaveAsync();
        string kidJwks = jwk.GetProperty("kid").GetString()!;

        string accessToken = await EmitirAccessTokenAsync();
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        token.Header.Kid.Should().Be(kidJwks);
    }

    private async Task<JsonElement> ObterPrimeiraChaveAsync()
    {
        string corpo = await _client.GetStringAsync("/.well-known/jwks.json");
        using var documento = JsonDocument.Parse(corpo);
        return documento.RootElement.GetProperty("keys")[0].Clone();
    }

    private async Task<string> EmitirAccessTokenAsync()
    {
        HttpResponseMessage cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios",
            new CadastrarUsuarioRequest(NomeUsuario, EmailUsuario, SenhaUsuario)
        );
        cadastro.EnsureSuccessStatusCode();

        HttpResponseMessage login = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(EmailUsuario, SenhaUsuario)
        );
        login.EnsureSuccessStatusCode();

        LoginResponse? body = await login.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
        return body!.AccessToken;
    }
}
