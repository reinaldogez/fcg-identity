using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.DTOs;
using FCG.Domain.Entities;
using FCG.Domain.Enums;
using FCG.Domain.ValueObjects;
using FCG.Infrastructure.Persistence;
using FCG.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Tests.Integration.Api;

public class AdminRelatoriosTests : IClassFixture<FcgApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly FcgApiFactory _factory;
    private readonly HttpClient _client;

    public AdminRelatoriosTests(FcgApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public Task InitializeAsync() => _factory.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeveRetornar401SemToken()
    {
        HttpResponseMessage resposta = await _client.GetAsync("/api/admin/relatorios/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403ParaUsuarioComum()
    {
        (Guid _, string token) = await _factory.CriarUsuarioAutenticadoAsync("comum@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.GetAsync("/api/admin/relatorios/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveRetornarAgregacoesCorretasParaAdministrador()
    {
        await SemearUsuariosAsync();

        (Guid _, string adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-relatorio@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.GetAsync("/api/admin/relatorios/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        RelatorioUsuariosDto? body = await resposta.Content.ReadFromJsonAsync<RelatorioUsuariosDto>(
            _jsonOptions
        );

        body.Should().NotBeNull();

        // Seed: 4 usuarios + 2 admins (incluindo o admin criado pra autenticar a chamada) = 6
        body!.TotalUsuarios.Should().Be(6);

        // 1 inativo entre os semeados → 5 ativos
        body.TotalAtivos.Should().Be(5);
        body.TotalInativos.Should().Be(1);
        body.PorTipo.Usuario.Should().Be(4);
        body.PorTipo.Administrador.Should().Be(2);

        // Todos foram criados agora → todos caem em "ultimos 30 dias"
        body.CadastrosUltimos30Dias.Should().Be(6);
        body.CadastrosPorMes.Should().NotBeEmpty();
    }

    private async Task SemearUsuariosAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        FcgDbContext contexto = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        Application.Interfaces.ISenhaService senhaService =
            scope.ServiceProvider.GetRequiredService<Application.Interfaces.ISenhaService>();

        SenhaHash hash = senhaService.GerarHash("Senha@123");

        var u1 = Usuario.Criar("Comum 1", Email.Criar("u1@fcg.com"), hash);
        var u2 = Usuario.Criar("Comum 2", Email.Criar("u2@fcg.com"), hash);
        var u3 = Usuario.Criar("Comum 3", Email.Criar("u3@fcg.com"), hash);
        var u4 = Usuario.Criar("Comum 4", Email.Criar("u4@fcg.com"), hash);
        u4.Desativar();
        var admin1 = Usuario.Criar(
            "Admin 1",
            Email.Criar("admin1@fcg.com"),
            hash,
            TipoUsuario.Administrador
        );

        await contexto.Usuarios.AddRangeAsync(u1, u2, u3, u4, admin1);
        await contexto.SaveChangesAsync();
    }
}
