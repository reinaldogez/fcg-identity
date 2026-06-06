using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Domain.Enums;
using Fcg.Identity.Infrastructure.Persistence;
using Fcg.Identity.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Identity.Tests.Integration.Api;

public class UsuarioEndpointsTests : IClassFixture<IdentityApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityApiFactory _factory;
    private readonly HttpClient _client;

    public UsuarioEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public Task InitializeAsync() => _factory.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // --- Cadastrar (público) ---

    [Fact]
    public async Task DeveCadastrarUsuarioERetornar201ComLocationHeader()
    {
        var request = new CadastrarUsuarioRequest(
            "Reinaldo Teste",
            "reinaldo@fcg.com",
            "Senha@123"
        );

        HttpResponseMessage resposta = await _client.PostAsJsonAsync("/api/usuarios", request);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location.Should().NotBeNull();
        resposta.Headers.Location!.ToString().Should().Contain("/api/usuarios/");

        UsuarioResponse? body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(
            _jsonOptions
        );
        body.Should().NotBeNull();
        body!.Id.Should().NotBe(Guid.Empty);
        body.Nome.Should().Be("Reinaldo Teste");
        body.Email.Should().Be("reinaldo@fcg.com");
        body.Tipo.Should().Be("Usuario");
        body.Ativo.Should().BeTrue();

        using IServiceScope scope = _factory.Services.CreateScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        int totalNoBanco = await context.Usuarios.CountAsync();
        totalNoBanco.Should().Be(1);
    }

    [Fact]
    public async Task DeveRetornar409QuandoEmailJaExiste()
    {
        var request = new CadastrarUsuarioRequest("Primeiro", "duplicado@fcg.com", "Senha@123");
        HttpResponseMessage primeiroCadastro = await _client.PostAsJsonAsync(
            "/api/usuarios",
            request
        );
        primeiroCadastro.EnsureSuccessStatusCode();

        var duplicado = new CadastrarUsuarioRequest("Segundo", "duplicado@fcg.com", "Senha@123");
        HttpResponseMessage resposta = await _client.PostAsJsonAsync("/api/usuarios", duplicado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
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

        HttpResponseMessage resposta = await _client.PostAsJsonAsync("/api/usuarios", request);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Obter por ID (OwnerOrAdmin) ---

    [Fact]
    public async Task DeveObterUsuarioPorIdComoDono()
    {
        (Guid id, string? token) = await _factory.CriarUsuarioAutenticadoAsync(
            "busca@fcg.com",
            nome: "Busca"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.GetAsync($"/api/usuarios/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        UsuarioResponse? body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(
            _jsonOptions
        );
        body!.Email.Should().Be("busca@fcg.com");
    }

    [Fact]
    public async Task DeveObterUsuarioPorIdComoAdmin()
    {
        (Guid idAlvo, string _) = await _factory.CriarUsuarioAutenticadoAsync("alvo@fcg.com");
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-obter@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.GetAsync($"/api/usuarios/{idAlvo}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        UsuarioResponse? body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(
            _jsonOptions
        );
        body!.Email.Should().Be("alvo@fcg.com");
    }

    [Fact]
    public async Task DeveRetornar401AoObterUsuarioPorIdSemToken()
    {
        (Guid id, string _) = await _factory.CriarUsuarioAutenticadoAsync("sem-token@fcg.com");

        HttpResponseMessage resposta = await _client.GetAsync($"/api/usuarios/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoObterUsuarioPorIdDeOutroUsuario()
    {
        (Guid idAlvo, string _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-cross@fcg.com");
        (Guid _, string? tokenOutro) = await _factory.CriarUsuarioAutenticadoAsync("outro@fcg.com");
        HttpClient clientOutro = _factory.CreateAuthenticatedClient(tokenOutro);

        HttpResponseMessage resposta = await clientOutro.GetAsync($"/api/usuarios/{idAlvo}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveRetornar404ParaUsuarioInexistente()
    {
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-404@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.GetAsync(
            $"/api/usuarios/{Guid.NewGuid()}"
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Listar (somente Administrador) ---

    [Fact]
    public async Task DeveRetornar401AoListarSemToken()
    {
        HttpResponseMessage resposta = await _client.GetAsync("/api/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoListarComoUsuarioComum()
    {
        (Guid _, string? token) = await _factory.CriarUsuarioAutenticadoAsync(
            "comum-listar@fcg.com"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.GetAsync("/api/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveListarUsuariosPaginadoComoAdmin()
    {
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-listar@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        await _factory.CriarUsuarioAutenticadoAsync("a@fcg.com");
        await _factory.CriarUsuarioAutenticadoAsync("b@fcg.com");
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.GetAsync(
            "/api/usuarios?pagina=1&tamanhoPagina=2"
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        ListarUsuariosResponse? body =
            await resposta.Content.ReadFromJsonAsync<ListarUsuariosResponse>(_jsonOptions);
        body!.Items.Should().HaveCount(2);
        body.Total.Should().Be(3);
    }

    [Theory]
    [InlineData("?pagina=0")]
    [InlineData("?pagina=-1")]
    [InlineData("?tamanhoPagina=0")]
    [InlineData("?tamanhoPagina=101")]
    public async Task DeveRetornar400ParaParametrosInvalidos(string queryString)
    {
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-q@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.GetAsync($"/api/usuarios{queryString}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Atualizar (OwnerOrAdmin) ---

    [Fact]
    public async Task DeveRetornar401AoAtualizarSemToken()
    {
        HttpResponseMessage resposta = await _client.PutAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}",
            new AtualizarUsuarioRequest("Nome", "x@fcg.com")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DevePermitirUsuarioComumAtualizarPropriosDados()
    {
        (Guid id, string? token) = await _factory.CriarUsuarioAutenticadoAsync(
            "dono-atualizar@fcg.com"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{id}",
            new AtualizarUsuarioRequest("Atualizado", "atualizado@fcg.com")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        UsuarioResponse? body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(
            _jsonOptions
        );
        body!.Nome.Should().Be("Atualizado");
        body.Email.Should().Be("atualizado@fcg.com");
    }

    [Fact]
    public async Task DeveRetornar403QuandoUsuarioComumTentaAtualizarOutroUsuario()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync("alvo@fcg.com");
        (Guid _, string? intrusoToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "intruso@fcg.com"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(intrusoToken);

        HttpResponseMessage resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{alvoId}",
            new AtualizarUsuarioRequest("Hackeado", "hack@fcg.com")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DevePermitirAdminAtualizarQualquerUsuario()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-admin@fcg.com");
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-atualizar@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PutAsJsonAsync(
            $"/api/usuarios/{alvoId}",
            new AtualizarUsuarioRequest("Renomeado pelo admin", "renomeado@fcg.com")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeveRetornar404AoAtualizarUsuarioInexistenteComoAdmin()
    {
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-404@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PutAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}",
            new AtualizarUsuarioRequest("Nome", "email@fcg.com")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveRetornar400AoAtualizarComEmailInvalido()
    {
        (Guid id, string? token) = await _factory.CriarUsuarioAutenticadoAsync("emailinv@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{id}",
            new AtualizarUsuarioRequest("Nome", "email-invalido")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar409AoAtualizarComEmailJaUsadoPorOutroUsuario()
    {
        (Guid id1, string? token1) = await _factory.CriarUsuarioAutenticadoAsync("um@fcg.com");
        await _factory.CriarUsuarioAutenticadoAsync("dois@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token1);

        HttpResponseMessage resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{id1}",
            new AtualizarUsuarioRequest("Um", "dois@fcg.com")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DevePermitirAtualizarComMesmoEmail()
    {
        (Guid id, string? token) = await _factory.CriarUsuarioAutenticadoAsync(
            "mesmoemail@fcg.com"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{id}",
            new AtualizarUsuarioRequest("Nome Atualizado", "mesmoemail@fcg.com")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- Alterar Senha (OwnerOrAdmin) ---

    [Fact]
    public async Task DeveRetornar401AoAlterarSenhaSemToken()
    {
        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DevePermitirUsuarioComumAlterarPropriaSenha()
    {
        (Guid id, string? token) = await _factory.CriarUsuarioAutenticadoAsync("propsenha@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.PostAsJsonAsync(
            $"/api/usuarios/{id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRetornar403QuandoUsuarioComumTentaAlterarSenhaDeOutroUsuario()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync("alvosenha@fcg.com");
        (Guid _, string? intrusoToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "intrusosenha@fcg.com"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(intrusoToken);

        HttpResponseMessage resposta = await client.PostAsJsonAsync(
            $"/api/usuarios/{alvoId}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "Hackeada@456")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveRetornar404AoAlterarSenhaDeUsuarioInexistenteComoAdmin()
    {
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-senha-404@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PostAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveRetornar400AoAlterarSenhaComSenhaAtualIncorreta()
    {
        (Guid id, string? token) = await _factory.CriarUsuarioAutenticadoAsync("errada@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.PostAsJsonAsync(
            $"/api/usuarios/{id}/alterar-senha",
            new AlterarSenhaRequest("SenhaErrada@1", "NovaSenha@456")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400AoAlterarSenhaComNovaSenhaFraca()
    {
        (Guid id, string? token) = await _factory.CriarUsuarioAutenticadoAsync("fraca@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        HttpResponseMessage resposta = await client.PostAsJsonAsync(
            $"/api/usuarios/{id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "fraca")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveAtualizarHashNoBancoAposAlterarSenha()
    {
        (Guid id, string? token) = await _factory.CriarUsuarioAutenticadoAsync("hash@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        using IServiceScope scopeAntes = _factory.Services.CreateScope();
        IdentityDbContext contextAntes =
            scopeAntes.ServiceProvider.GetRequiredService<IdentityDbContext>();
        string hashAntes = (await contextAntes.Usuarios.FindAsync(id))!.SenhaHash.Valor;

        await client.PostAsJsonAsync(
            $"/api/usuarios/{id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456")
        );

        using IServiceScope scopeDepois = _factory.Services.CreateScope();
        IdentityDbContext contextDepois =
            scopeDepois.ServiceProvider.GetRequiredService<IdentityDbContext>();
        string hashDepois = (await contextDepois.Usuarios.FindAsync(id))!.SenhaHash.Valor;

        hashDepois.Should().NotBe(hashAntes);
    }

    // --- Desativar (somente Administrador) ---

    [Fact]
    public async Task DeveRetornar401AoDesativarSemToken()
    {
        HttpResponseMessage resposta = await _client.PatchAsync(
            $"/api/usuarios/{Guid.NewGuid()}/desativar",
            null
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoDesativarComoUsuarioComum()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync(
            "alvo-desativar@fcg.com"
        );
        (Guid _, string? comumToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "comum-desativar@fcg.com"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(comumToken);

        HttpResponseMessage resposta = await client.PatchAsync(
            $"/api/usuarios/{alvoId}/desativar",
            null
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveDesativarUsuarioComoAdminERetornar204()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync(
            "desativaralvo@fcg.com"
        );
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-desat@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PatchAsync(
            $"/api/usuarios/{alvoId}/desativar",
            null
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRetornar404AoDesativarUsuarioInexistenteComoAdmin()
    {
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-desat-404@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PatchAsync(
            $"/api/usuarios/{Guid.NewGuid()}/desativar",
            null
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveSerIdempotenteAoDesativarDuasVezesComoAdmin()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync(
            "idempotente@fcg.com"
        );
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-idem@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        await adminClient.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);
        HttpResponseMessage segunda = await adminClient.PatchAsync(
            $"/api/usuarios/{alvoId}/desativar",
            null
        );

        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRefletirAtivoFalseNoGetAposDesativar()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync("ativofalso@fcg.com");
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-ativofalso@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        await adminClient.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);

        HttpResponseMessage get = await adminClient.GetAsync($"/api/usuarios/{alvoId}");
        UsuarioResponse? body = await get.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Ativo.Should().BeFalse();
    }

    // --- Ativar (somente Administrador) ---

    [Fact]
    public async Task DeveRetornar401AoAtivarSemToken()
    {
        HttpResponseMessage resposta = await _client.PatchAsync(
            $"/api/usuarios/{Guid.NewGuid()}/ativar",
            null
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoAtivarComoUsuarioComum()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync(
            "alvo-ativar@fcg.com"
        );
        (Guid _, string? comumToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "comum-ativar@fcg.com"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(comumToken);

        HttpResponseMessage resposta = await client.PatchAsync(
            $"/api/usuarios/{alvoId}/ativar",
            null
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveAtivarUsuarioComoAdminERetornar204()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync("ativaralvo@fcg.com");
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-ativ@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);
        await adminClient.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);

        HttpResponseMessage resposta = await adminClient.PatchAsync(
            $"/api/usuarios/{alvoId}/ativar",
            null
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRetornar404AoAtivarUsuarioInexistenteComoAdmin()
    {
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-ativ-404@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PatchAsync(
            $"/api/usuarios/{Guid.NewGuid()}/ativar",
            null
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveSerIdempotenteAoAtivarDuasVezesComoAdmin()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync(
            "idempotente-ativ@fcg.com"
        );
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-idem-ativ@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        await adminClient.PatchAsync($"/api/usuarios/{alvoId}/ativar", null);
        HttpResponseMessage segunda = await adminClient.PatchAsync(
            $"/api/usuarios/{alvoId}/ativar",
            null
        );

        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRefletirAtivoTrueNoGetAposAtivar()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync("ativotrue@fcg.com");
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-ativotrue@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);
        await adminClient.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);

        await adminClient.PatchAsync($"/api/usuarios/{alvoId}/ativar", null);

        HttpResponseMessage get = await adminClient.GetAsync($"/api/usuarios/{alvoId}");
        UsuarioResponse? body = await get.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Ativo.Should().BeTrue();
    }

    // --- Alterar Tipo (somente Administrador) ---

    [Fact]
    public async Task DeveRetornar401AoAlterarTipoSemToken()
    {
        HttpResponseMessage resposta = await _client.PatchAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/tipo",
            new AlterarTipoRequest(TipoUsuario.Administrador)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoAlterarTipoComoUsuarioComum()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-tipo@fcg.com");
        (Guid _, string? comumToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "comum-tipo@fcg.com"
        );
        HttpClient client = _factory.CreateAuthenticatedClient(comumToken);

        HttpResponseMessage resposta = await client.PatchAsJsonAsync(
            $"/api/usuarios/{alvoId}/tipo",
            new AlterarTipoRequest(TipoUsuario.Administrador)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveAlterarTipoParaAdministradorComoAdmin()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync(
            "alvo-promover@fcg.com"
        );
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-promover@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{alvoId}/tipo",
            new AlterarTipoRequest(TipoUsuario.Administrador)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        UsuarioResponse? body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(
            _jsonOptions
        );
        body!.Tipo.Should().Be("Administrador");
    }

    [Fact]
    public async Task DeveAlterarTipoParaUsuarioRebaixandoOutroAdmin()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync(
            "outro-admin@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-rebaixa@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{alvoId}/tipo",
            new AlterarTipoRequest(TipoUsuario.Usuario)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        UsuarioResponse? body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(
            _jsonOptions
        );
        body!.Tipo.Should().Be("Usuario");
    }

    [Fact]
    public async Task NaoDevePermitirAdminRebaixarASiMesmo()
    {
        (Guid adminId, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "autorebaixar@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{adminId}/tipo",
            new AlterarTipoRequest(TipoUsuario.Usuario)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        RespostaErro? erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(_jsonOptions);
        erro!.Errors[0].Should().Contain("rebaixar a si mesmo");
    }

    [Fact]
    public async Task DeveRetornar400ParaTipoInvalido()
    {
        (Guid alvoId, string _) = await _factory.CriarUsuarioAutenticadoAsync(
            "alvo-tipo-inv@fcg.com"
        );
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-tipo-inv@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        using var conteudo = new System.Net.Http.StringContent(
            "{\"tipo\":\"Root\"}",
            System.Text.Encoding.UTF8,
            "application/json"
        );
        HttpResponseMessage resposta = await adminClient.PatchAsync(
            $"/api/usuarios/{alvoId}/tipo",
            conteudo
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar404AoAlterarTipoDeUsuarioInexistente()
    {
        (Guid _, string? adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-tipo-404@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        HttpResponseMessage resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/tipo",
            new AlterarTipoRequest(TipoUsuario.Administrador)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record RespostaErro(string Type, string Title, int Status, List<string> Errors);
}
