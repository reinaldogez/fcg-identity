using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.DTOs;
using FCG.Tests.Bdd.Support;
using FluentAssertions;
using Reqnroll;

namespace FCG.Tests.Bdd.Steps;

[Binding]
public class CadastroUsuarioSteps(HttpClient client, CenarioEstado estado)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Given(@"que ja existe um usuario com email ""(.*)"" e senha ""(.*)""")]
    public async Task DadoQueJaExisteUmUsuarioComEmailESenha(string email, string senha)
    {
        var request = new CadastrarUsuarioRequest("Usuario Existente", email, senha);
        HttpResponseMessage resposta = await client.PostAsJsonAsync("/api/usuarios", request);
        resposta.IsSuccessStatusCode.Should().BeTrue(
            $"pré-condição: cadastro de '{email}' deveria ter retornado 2xx, mas retornou {(int)resposta.StatusCode}");
    }

    [When(@"eu cadastro um usuario com nome ""(.*)"", email ""(.*)"" e senha ""(.*)""")]
    public async Task QuandoEuCadastroUmUsuarioComNomeEmailESenha(string nome, string email, string senha)
    {
        var request = new CadastrarUsuarioRequest(nome, email, senha);
        estado.UltimaResposta = await client.PostAsJsonAsync("/api/usuarios", request);
    }

    [Then(@"o corpo da resposta contem o id do usuario")]
    public async Task EntaoOCorpoDaRespostaContemOIdDoUsuario()
    {
        estado.UltimaResposta.Should().NotBeNull();
        string json = await estado.UltimaResposta!.Content.ReadAsStringAsync();
        UsuarioResponse? usuario = JsonSerializer.Deserialize<UsuarioResponse>(json, JsonOptions);
        usuario.Should().NotBeNull();
        usuario!.Id.Should().NotBe(Guid.Empty);
    }
}
