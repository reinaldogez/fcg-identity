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
        erro!.Type.Should().Be("ErroDeNegocio");
        erro.Status.Should().Be(409);
        erro.Errors.Should().NotBeEmpty();
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
        erro!.Type.Should().Be("ErroDeValidacao");
        erro.Status.Should().Be(400);
        erro.Errors.Should().NotBeEmpty();
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

    // --- Listar ---

    [Fact]
    public async Task DeveListarUsuariosPaginadoComTotalCorreto()
    {
        await _client.PostAsJsonAsync("/api/usuarios", new CadastrarUsuarioRequest("A", "a@fcg.com", "Senha@123"));
        await _client.PostAsJsonAsync("/api/usuarios", new CadastrarUsuarioRequest("B", "b@fcg.com", "Senha@123"));
        await _client.PostAsJsonAsync("/api/usuarios", new CadastrarUsuarioRequest("C", "c@fcg.com", "Senha@123"));

        var resposta = await _client.GetAsync("/api/usuarios?pagina=1&tamanhoPagina=2");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<ListarUsuariosResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
        body.Total.Should().Be(3);
        body.Pagina.Should().Be(1);
        body.TamanhoPagina.Should().Be(2);
    }

    [Fact]
    public async Task DeveRetornarListaVaziaQuandoNaoHaUsuarios()
    {
        var resposta = await _client.GetAsync("/api/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<ListarUsuariosResponse>(_jsonOptions);
        body!.Items.Should().BeEmpty();
        body.Total.Should().Be(0);
    }

    [Theory]
    [InlineData("?pagina=0")]
    [InlineData("?pagina=-1")]
    public async Task DeveRetornar400ParaPaginaInvalida(string queryString)
    {
        var resposta = await _client.GetAsync($"/api/usuarios{queryString}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("?tamanhoPagina=0")]
    [InlineData("?tamanhoPagina=101")]
    public async Task DeveRetornar400ParaTamanhoPaginaInvalido(string queryString)
    {
        var resposta = await _client.GetAsync($"/api/usuarios{queryString}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Atualizar ---

    [Fact]
    public async Task DeveAtualizarNomeEEmailERetornar200()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Original", "original@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PutAsJsonAsync(
            $"/api/usuarios/{usuario!.Id}",
            new AtualizarUsuarioRequest("Atualizado", "atualizado@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Nome.Should().Be("Atualizado");
        body.Email.Should().Be("atualizado@fcg.com");
    }

    [Fact]
    public async Task DeveRetornar404AoAtualizarUsuarioInexistente()
    {
        var resposta = await _client.PutAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}",
            new AtualizarUsuarioRequest("Nome", "email@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveRetornar400AoAtualizarComEmailInvalido()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "nome@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PutAsJsonAsync(
            $"/api/usuarios/{usuario!.Id}",
            new AtualizarUsuarioRequest("Nome", "email-invalido"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar409AoAtualizarComEmailJaUsadoPorOutroUsuario()
    {
        var cadastro1 = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Um", "um@fcg.com", "Senha@123"));
        var usuario1 = await cadastro1.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Dois", "dois@fcg.com", "Senha@123"));

        var resposta = await _client.PutAsJsonAsync(
            $"/api/usuarios/{usuario1!.Id}",
            new AtualizarUsuarioRequest("Um", "dois@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DevePermitirAtualizarComMesmoEmail()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "mesmoemail@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PutAsJsonAsync(
            $"/api/usuarios/{usuario!.Id}",
            new AtualizarUsuarioRequest("Nome Atualizado", "mesmoemail@fcg.com"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Nome.Should().Be("Nome Atualizado");
    }

    // --- Alterar Senha ---

    [Fact]
    public async Task DeveAlterarSenhaERetornar204()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "senha@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PostAsJsonAsync(
            $"/api/usuarios/{usuario!.Id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRetornar404AoAlterarSenhaDeUsuarioInexistente()
    {
        var resposta = await _client.PostAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveRetornar400AoAlterarSenhaComSenhaAtualIncorreta()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "errada@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PostAsJsonAsync(
            $"/api/usuarios/{usuario!.Id}/alterar-senha",
            new AlterarSenhaRequest("SenhaErrada@1", "NovaSenha@456"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400AoAlterarSenhaComNovaSenhaFraca()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "fraca@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PostAsJsonAsync(
            $"/api/usuarios/{usuario!.Id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "fraca"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveAtualizarHashNoBancoAposAlterarSenha()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "hash@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        using var scopeAntes = _factory.Services.CreateScope();
        var contextAntes = scopeAntes.ServiceProvider.GetRequiredService<FcgDbContext>();
        var hashAntes = (await contextAntes.Usuarios.FindAsync(usuario!.Id))!.SenhaHash.Valor;

        await _client.PostAsJsonAsync(
            $"/api/usuarios/{usuario.Id}/alterar-senha",
            new AlterarSenhaRequest("Senha@123", "NovaSenha@456"));

        using var scopeDepois = _factory.Services.CreateScope();
        var contextDepois = scopeDepois.ServiceProvider.GetRequiredService<FcgDbContext>();
        var hashDepois = (await contextDepois.Usuarios.FindAsync(usuario.Id))!.SenhaHash.Valor;

        hashDepois.Should().NotBe(hashAntes);
    }

    // --- Desativar ---

    [Fact]
    public async Task DeveDesativarUsuarioERetornar204()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "desativar@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PatchAsync(
            $"/api/usuarios/{usuario!.Id}/desativar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRetornar404AoDesativarUsuarioInexistente()
    {
        var resposta = await _client.PatchAsync(
            $"/api/usuarios/{Guid.NewGuid()}/desativar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeveSerIdempotenteAoDesativarDuasVezes()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "idempotente@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        await _client.PatchAsync($"/api/usuarios/{usuario!.Id}/desativar", null);
        var segunda = await _client.PatchAsync($"/api/usuarios/{usuario.Id}/desativar", null);

        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRefletirAtivoFalseNoGetAposDesativar()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "ativofalso@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        await _client.PatchAsync($"/api/usuarios/{usuario!.Id}/desativar", null);

        var get = await _client.GetAsync($"/api/usuarios/{usuario.Id}");
        var body = await get.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Ativo.Should().BeFalse();
    }

    // --- Alterar Tipo ---

    [Fact]
    public async Task DeveAlterarTipoParaAdministradorERetornar200()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "tipo@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PatchAsJsonAsync(
            $"/api/usuarios/{usuario!.Id}/tipo",
            new AlterarTipoRequest("Administrador"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Tipo.Should().Be("Administrador");
    }

    [Fact]
    public async Task DeveAlterarTipoParaUsuarioERetornar200()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "rebaixar@fcg.com", "Senha@123"));
        var admin = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        await _client.PatchAsJsonAsync($"/api/usuarios/{admin!.Id}/tipo", new AlterarTipoRequest("Administrador"));

        var resposta = await _client.PatchAsJsonAsync(
            $"/api/usuarios/{admin.Id}/tipo",
            new AlterarTipoRequest("Usuario"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);
        body!.Tipo.Should().Be("Usuario");
    }

    [Fact]
    public async Task DeveRetornar400ParaTipoInvalido()
    {
        var cadastro = await _client.PostAsJsonAsync(
            "/api/usuarios", new CadastrarUsuarioRequest("Nome", "tipoinvalido@fcg.com", "Senha@123"));
        var usuario = await cadastro.Content.ReadFromJsonAsync<UsuarioResponse>(_jsonOptions);

        var resposta = await _client.PatchAsJsonAsync(
            $"/api/usuarios/{usuario!.Id}/tipo",
            new AlterarTipoRequest("Root"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar404AoAlterarTipoDeUsuarioInexistente()
    {
        var resposta = await _client.PatchAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/tipo",
            new AlterarTipoRequest("Administrador"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record RespostaErro(string Type, string Title, int Status, List<string> Errors);
}
