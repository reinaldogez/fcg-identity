using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.DTOs;
using FCG.Infrastructure.Persistence;
using FCG.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Tests.Integration.Api;

public class UsuarioEndpointsTests : IClassFixture<FcgApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly FcgApiFactory _factory;
    private readonly HttpClient _client;

    public UsuarioEndpointsTests(FcgApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public Task InitializeAsync() => _factory.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeveCadastrarUsuarioERetornar201ComLocationHeader()
    {
        var request = new CadastrarUsuarioRequest("Reinaldo Teste", "reinaldo@fcg.com", "Senha@123");

        var resposta = await _client.PostAsJsonAsync("/api/usuarios", request);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location.Should().NotBeNull();
        resposta.Headers.Location!.ToString().Should().Contain("/api/usuarios/");

        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().NotBe(Guid.Empty);
        body.Nome.Should().Be("Reinaldo Teste");
        body.Email.Should().Be("reinaldo@fcg.com");
        body.Tipo.Should().Be("Usuario");
        body.Ativo.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var totalNoBanco = await context.Usuarios.CountAsync();
        totalNoBanco.Should().Be(1);
    }

    [Fact]
    public async Task DeveRetornar409QuandoEmailJaExiste()
    {
        var request = new CadastrarUsuarioRequest("Primeiro", "duplicado@fcg.com", "Senha@123");
        var primeiroCadastro = await _client.PostAsJsonAsync("/api/usuarios", request);
        primeiroCadastro.EnsureSuccessStatusCode();

        var duplicado = new CadastrarUsuarioRequest("Segundo", "duplicado@fcg.com", "Senha@123");
        var resposta = await _client.PostAsJsonAsync("/api/usuarios", duplicado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(_jsonOptions);
        erro.Should().NotBeNull();
        erro!.Tipo.Should().Be("ErroDeNegocio");
        erro.Status.Should().Be(409);
        erro.Erros.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("Nome Valido", "email-invalido", "Senha@123")]
    [InlineData("Nome Valido", "valido@fcg.com", "Ab@1")]
    [InlineData("Nome Valido", "valido@fcg.com", "SenhaSimples1")]
    [InlineData("Nome Valido", "valido@fcg.com", "SenhaForte@")]
    [InlineData("", "valido@fcg.com", "Senha@123")]
    public async Task DeveRetornar400ParaRequestInvalido(string nome, string email, string senha)
    {
        var request = new CadastrarUsuarioRequest(nome, email, senha);

        var resposta = await _client.PostAsJsonAsync("/api/usuarios", request);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(_jsonOptions);
        erro.Should().NotBeNull();
        erro!.Tipo.Should().Be("ErroDeValidacao");
        erro.Status.Should().Be(400);
        erro.Erros.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeveObterUsuarioPorIdAposCadastro()
    {
        var request = new CadastrarUsuarioRequest("Busca", "busca@fcg.com", "Senha@123");
        var cadastro = await _client.PostAsJsonAsync("/api/usuarios", request);
        var cadastrado = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.GetAsync($"/api/usuarios/{cadastrado!.Id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().Be(cadastrado.Id);
        body.Email.Should().Be("busca@fcg.com");
        body.Nome.Should().Be("Busca");
    }

    [Fact]
    public async Task DeveRetornar404ParaUsuarioInexistente()
    {
        var resposta = await _client.GetAsync($"/api/usuarios/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record RespostaErro(string Tipo, string Titulo, int Status, List<string> Erros);
}
