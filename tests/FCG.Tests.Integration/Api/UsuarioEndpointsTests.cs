using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.DTOs;
using FCG.Domain.Enums;
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

    // --- Cadastrar (público) ---

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
    }

    // --- Obter por ID (OwnerOrAdmin) ---

    [Fact]
    public async Task DeveObterUsuarioPorIdComoDono()
    {
        var (id, token) = await _factory.CriarUsuarioAutenticadoAsync("busca@fcg.com", nome: "Busca");
        var client = _factory.CreateAuthenticatedClient(token);

        var resposta = await client.GetAsync($"/api/usuarios/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Email.Should().Be("busca@fcg.com");
    }

    [Fact]
    public async Task DeveObterUsuarioPorIdComoAdmin()
    {
        var (idAlvo, _) = await _factory.CriarUsuarioAutenticadoAsync("alvo@fcg.com");
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-obter@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.GetAsync($"/api/usuarios/{idAlvo}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Email.Should().Be("alvo@fcg.com");
    }

    [Fact]
    public async Task DeveRetornar401AoObterUsuarioPorIdSemToken()
    {
        var (id, _) = await _factory.CriarUsuarioAutenticadoAsync("sem-token@fcg.com");

        var resposta = await _client.GetAsync($"/api/usuarios/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoObterUsuarioPorIdDeOutroUsuario()
    {
        var (idAlvo, _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-cross@fcg.com");
        var (_, tokenOutro) = await _factory.CriarUsuarioAutenticadoAsync("outro@fcg.com");
        var clientOutro = _factory.CreateAuthenticatedClient(tokenOutro);

        var resposta = await clientOutro.GetAsync($"/api/usuarios/{idAlvo}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveRetornar404ParaUsuarioInexistente()
    {
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-404@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.GetAsync($"/api/usuarios/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Listar (somente Administrador) ---

    [Fact]
    public async Task DeveRetornar401AoListarSemToken()
    {
        var resposta = await _client.GetAsync("/api/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoListarComoUsuarioComum()
    {
        var (_, token) = await _factory.CriarUsuarioAutenticadoAsync("comum-listar@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token);

        var resposta = await client.GetAsync("/api/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveListarUsuariosPaginadoComoAdmin()
    {
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-listar@fcg.com", tipo: TipoUsuario.Administrador);
        await _factory.CriarUsuarioAutenticadoAsync("a@fcg.com");
        await _factory.CriarUsuarioAutenticadoAsync("b@fcg.com");
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.GetAsync("/api/usuarios?pagina=1&tamanhoPagina=2");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<ListarUsuariosResponse>(_jsonOptions);
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
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-q@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.GetAsync($"/api/usuarios{queryString}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Atualizar (OwnerOrAdmin) ---

    [Fact]
    public async Task DeveRetornar401AoAtualizarSemToken()
    {
        var resposta = await _client.PutAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}",
            new AtualizarUsuarioRequest("Nome", "x@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DevePermitirUsuarioComumAtualizarPropriosDados()
    {
        var (id, token) = await _factory.CriarUsuarioAutenticadoAsync("dono-atualizar@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token);

        var resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{id}",
            new AtualizarUsuarioRequest("Atualizado", "atualizado@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Nome.Should().Be("Atualizado");
        body.Email.Should().Be("atualizado@fcg.com");
    }

    [Fact]
    public async Task DeveRetornar403QuandoUsuarioComumTentaAtualizarOutroUsuario()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("alvo@fcg.com");
        var (_, intrusoToken) = await _factory.CriarUsuarioAutenticadoAsync("intruso@fcg.com");
        var client = _factory.CreateAuthenticatedClient(intrusoToken);

        var resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{alvoId}",
            new AtualizarUsuarioRequest("Hackeado", "hack@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DevePermitirAdminAtualizarQualquerUsuario()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-admin@fcg.com");
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-atualizar@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PutAsJsonAsync(
            $"/api/usuarios/{alvoId}",
            new AtualizarUsuarioRequest("Renomeado pelo admin", "renomeado@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeveRetornar404AoAtualizarUsuarioInexistenteComoAdmin()
    {
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-404@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PutAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}",
            new AtualizarUsuarioRequest("Nome", "email@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveRetornar400AoAtualizarComEmailInvalido()
    {
        var (id, token) = await _factory.CriarUsuarioAutenticadoAsync("emailinv@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token);

        var resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{id}",
            new AtualizarUsuarioRequest("Nome", "email-invalido"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar409AoAtualizarComEmailJaUsadoPorOutroUsuario()
    {
        var (id1, token1) = await _factory.CriarUsuarioAutenticadoAsync("um@fcg.com");
        await _factory.CriarUsuarioAutenticadoAsync("dois@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token1);

        var resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{id1}",
            new AtualizarUsuarioRequest("Um", "dois@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DevePermitirAtualizarComMesmoEmail()
    {
        var (id, token) = await _factory.CriarUsuarioAutenticadoAsync("mesmoemail@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token);

        var resposta = await client.PutAsJsonAsync(
            $"/api/usuarios/{id}",
            new AtualizarUsuarioRequest("Nome Atualizado", "mesmoemail@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- Alterar Senha (OwnerOrAdmin) ---

    [Fact]
    public async Task DeveRetornar401AoAlterarSenhaSemToken()
    {
        var resposta = await _client.PostAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DevePermitirUsuarioComumAlterarPropriaSenha()
    {
        var (id, token) = await _factory.CriarUsuarioAutenticadoAsync("propsenha@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token);

        var resposta = await client.PostAsJsonAsync(
            $"/api/usuarios/{id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRetornar403QuandoUsuarioComumTentaAlterarSenhaDeOutroUsuario()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("alvosenha@fcg.com");
        var (_, intrusoToken) = await _factory.CriarUsuarioAutenticadoAsync("intrusosenha@fcg.com");
        var client = _factory.CreateAuthenticatedClient(intrusoToken);

        var resposta = await client.PostAsJsonAsync(
            $"/api/usuarios/{alvoId}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "Hackeada@456"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveRetornar404AoAlterarSenhaDeUsuarioInexistenteComoAdmin()
    {
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-senha-404@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PostAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveRetornar400AoAlterarSenhaComSenhaAtualIncorreta()
    {
        var (id, token) = await _factory.CriarUsuarioAutenticadoAsync("errada@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token);

        var resposta = await client.PostAsJsonAsync(
            $"/api/usuarios/{id}/alterar-senha",
            new AlterarSenhaRequest("SenhaErrada@1", "NovaSenha@456"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400AoAlterarSenhaComNovaSenhaFraca()
    {
        var (id, token) = await _factory.CriarUsuarioAutenticadoAsync("fraca@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token);

        var resposta = await client.PostAsJsonAsync(
            $"/api/usuarios/{id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "fraca"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveAtualizarHashNoBancoAposAlterarSenha()
    {
        var (id, token) = await _factory.CriarUsuarioAutenticadoAsync("hash@fcg.com");
        var client = _factory.CreateAuthenticatedClient(token);

        using var scopeAntes = _factory.Services.CreateScope();
        var contextAntes = scopeAntes.ServiceProvider.GetRequiredService<FcgDbContext>();
        var hashAntes = (await contextAntes.Usuarios.FindAsync(id))!.SenhaHash.Valor;

        await client.PostAsJsonAsync(
            $"/api/usuarios/{id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456"));

        using var scopeDepois = _factory.Services.CreateScope();
        var contextDepois = scopeDepois.ServiceProvider.GetRequiredService<FcgDbContext>();
        var hashDepois = (await contextDepois.Usuarios.FindAsync(id))!.SenhaHash.Valor;

        hashDepois.Should().NotBe(hashAntes);
    }

    // --- Desativar (somente Administrador) ---

    [Fact]
    public async Task DeveRetornar401AoDesativarSemToken()
    {
        var resposta = await _client.PatchAsync(
            $"/api/usuarios/{Guid.NewGuid()}/desativar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoDesativarComoUsuarioComum()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-desativar@fcg.com");
        var (_, comumToken) = await _factory.CriarUsuarioAutenticadoAsync("comum-desativar@fcg.com");
        var client = _factory.CreateAuthenticatedClient(comumToken);

        var resposta = await client.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveDesativarUsuarioComoAdminERetornar204()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("desativaralvo@fcg.com");
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-desat@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRetornar404AoDesativarUsuarioInexistenteComoAdmin()
    {
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-desat-404@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PatchAsync($"/api/usuarios/{Guid.NewGuid()}/desativar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveSerIdempotenteAoDesativarDuasVezesComoAdmin()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("idempotente@fcg.com");
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-idem@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        await adminClient.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);
        var segunda = await adminClient.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);

        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRefletirAtivoFalseNoGetAposDesativar()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("ativofalso@fcg.com");
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-ativofalso@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        await adminClient.PatchAsync($"/api/usuarios/{alvoId}/desativar", null);

        var get = await adminClient.GetAsync($"/api/usuarios/{alvoId}");
        var body = await get.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Ativo.Should().BeFalse();
    }

    // --- Alterar Tipo (somente Administrador) ---

    [Fact]
    public async Task DeveRetornar401AoAlterarTipoSemToken()
    {
        var resposta = await _client.PatchAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/tipo",
            new AlterarTipoRequest("Administrador"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar403AoAlterarTipoComoUsuarioComum()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-tipo@fcg.com");
        var (_, comumToken) = await _factory.CriarUsuarioAutenticadoAsync("comum-tipo@fcg.com");
        var client = _factory.CreateAuthenticatedClient(comumToken);

        var resposta = await client.PatchAsJsonAsync(
            $"/api/usuarios/{alvoId}/tipo",
            new AlterarTipoRequest("Administrador"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveAlterarTipoParaAdministradorComoAdmin()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-promover@fcg.com");
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-promover@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{alvoId}/tipo",
            new AlterarTipoRequest("Administrador"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Tipo.Should().Be("Administrador");
    }

    [Fact]
    public async Task DeveAlterarTipoParaUsuarioRebaixandoOutroAdmin()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("outro-admin@fcg.com", tipo: TipoUsuario.Administrador);
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-rebaixa@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{alvoId}/tipo",
            new AlterarTipoRequest("Usuario"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Tipo.Should().Be("Usuario");
    }

    [Fact]
    public async Task NaoDevePermitirAdminRebaixarASiMesmo()
    {
        var (adminId, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("autorebaixar@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{adminId}/tipo",
            new AlterarTipoRequest("Usuario"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(_jsonOptions);
        erro!.Errors[0].Should().Contain("rebaixar a si mesmo");
    }

    [Fact]
    public async Task DeveRetornar400ParaTipoInvalido()
    {
        var (alvoId, _) = await _factory.CriarUsuarioAutenticadoAsync("alvo-tipo-inv@fcg.com");
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-tipo-inv@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{alvoId}/tipo",
            new AlterarTipoRequest("Root"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar404AoAlterarTipoDeUsuarioInexistente()
    {
        var (_, adminToken) = await _factory.CriarUsuarioAutenticadoAsync("admin-tipo-404@fcg.com", tipo: TipoUsuario.Administrador);
        var adminClient = _factory.CreateAuthenticatedClient(adminToken);

        var resposta = await adminClient.PatchAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/tipo",
            new AlterarTipoRequest("Administrador"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record RespostaErro(string Type, string Title, int Status, List<string> Errors);
}
