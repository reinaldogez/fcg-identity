using System.Net.Http.Json;
using System.Text.Json;
using FCG.Tests.Bdd.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Reqnroll;

namespace FCG.Tests.Bdd.Steps;

[Binding]
public class CommonSteps(CenarioEstado estado)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Then(@"recebo o status (.*)")]
    public void EntaoReceboOStatus(int statusEsperado)
    {
        estado.UltimaResposta.Should().NotBeNull();
        ((int)estado.UltimaResposta!.StatusCode).Should().Be(statusEsperado);
    }

    [Then(@"a mensagem de erro contem ""(.*)""")]
    public async Task EntaoAMensagemDeErroContem(string fragmento)
    {
        estado.UltimaResposta.Should().NotBeNull();
        string json = await estado.UltimaResposta!.Content.ReadAsStringAsync();
        ProblemDetails? problem = JsonSerializer.Deserialize<ProblemDetails>(json, JsonOptions);
        problem.Should().NotBeNull();

        string[] erros = problem!.Extensions["errors"]
            .As<JsonElement>()
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();

        erros.Should().Contain(e => e.Contains(fragmento, StringComparison.OrdinalIgnoreCase));
    }

    [Then(@"a mensagem de erro e ""(.*)""")]
    public async Task EntaoAMensagemDeErroE(string mensagemExata)
    {
        estado.UltimaResposta.Should().NotBeNull();
        string json = await estado.UltimaResposta!.Content.ReadAsStringAsync();
        ProblemDetails? problem = JsonSerializer.Deserialize<ProblemDetails>(json, JsonOptions);
        problem.Should().NotBeNull();

        string[] erros = problem!.Extensions["errors"]
            .As<JsonElement>()
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();

        erros.Should().Contain(mensagemExata);
    }
}
