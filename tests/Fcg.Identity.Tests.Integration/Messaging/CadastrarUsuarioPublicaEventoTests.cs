using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fcg.Contracts.Events;
using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Infrastructure.Persistence;
using Fcg.Identity.Infrastructure.Services;
using Fcg.Identity.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fcg.Identity.Tests.Integration.Messaging;

// Prova o comportamento de negócio da mensageria: o cadastro ao vivo grava o UserCreatedEvent no
// Outbox dentro da mesma transação do usuário, e nenhum outro caminho (cadastro falho, seed de
// desenvolvimento) gera evento. Os hosted services do bus não rodam no host de teste, então as
// linhas de OutboxMessage permanecem no banco, disponíveis para inspeção.
[Collection("Integration")]
public class CadastrarUsuarioPublicaEventoTests(IdentityApiFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public Task InitializeAsync() => factory.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CadastroDeveGravarUserCreatedEventNoOutboxComDadosDoUsuario()
    {
        HttpClient client = factory.CreateClient();
        var request = new CadastrarUsuarioRequest("Evento Teste", "evento@fcg.com", "Senha@123");

        HttpResponseMessage resposta = await client.PostAsJsonAsync("/api/usuarios", request);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        UsuarioResponse? body = await resposta.Content.ReadFromJsonAsync<UsuarioResponse>(
            _jsonOptions
        );

        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext contexto = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        List<string> corpos = await contexto
            .Database.SqlQueryRaw<string>("SELECT Body AS Value FROM OutboxMessage")
            .ToListAsync();

        corpos.Should().ContainSingle();

        // O Body é o envelope do MassTransit: o tipo vai em messageType (urn) e o payload em message.
        using var envelope = JsonDocument.Parse(corpos[0]);
        envelope
            .RootElement.GetProperty("messageType")[0]
            .GetString()
            .Should()
            .Contain("Fcg.Contracts.Events:UserCreatedEvent");

        UserCreatedEvent? evento = envelope
            .RootElement.GetProperty("message")
            .Deserialize<UserCreatedEvent>(_jsonOptions);

        evento.Should().NotBeNull();
        evento!.EventVersion.Should().Be(1);
        evento.UserId.Should().Be(body!.Id);
        evento.Name.Should().Be("Evento Teste");
        evento.Email.Should().Be("evento@fcg.com");
        evento.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CadastroFalhoNaoDeveDeixarUsuarioNemEventoNoOutbox()
    {
        HttpClient client = factory.CreateClient();
        var request = new CadastrarUsuarioRequest("Primeiro", "atomico@fcg.com", "Senha@123");
        HttpResponseMessage primeiroCadastro = await client.PostAsJsonAsync(
            "/api/usuarios",
            request
        );
        primeiroCadastro.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicado = new CadastrarUsuarioRequest("Segundo", "atomico@fcg.com", "Senha@123");
        HttpResponseMessage resposta = await client.PostAsJsonAsync("/api/usuarios", duplicado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Usuário e evento são atômicos: o cadastro rejeitado não persiste nem um nem outro —
        // sobra apenas o par do primeiro cadastro.
        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext contexto = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await contexto.Usuarios.CountAsync()).Should().Be(1);
        long linhasNoOutbox = await contexto
            .Database.SqlQueryRaw<long>("SELECT COUNT_BIG(*) AS Value FROM OutboxMessage")
            .SingleAsync();
        linhasNoOutbox.Should().Be(1);
    }

    [Fact]
    public async Task DevSeedNaoDeveGravarEventoNoOutbox()
    {
        // O DevSeedService não roda no ambiente Testing; é instanciado direto aqui para provar a
        // separação de papéis: o seed escreve no banco sem publicar — só o cadastro ao vivo emite.
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DevSeed:Enabled"] = "true" })
            .Build();
        var seed = new DevSeedService(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            configuracao,
            NullLogger<DevSeedService>.Instance
        );

        await seed.StartAsync(CancellationToken.None);

        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext contexto = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await contexto.Usuarios.CountAsync()).Should().BeGreaterThan(0);
        long linhasNoOutbox = await contexto
            .Database.SqlQueryRaw<long>("SELECT COUNT_BIG(*) AS Value FROM OutboxMessage")
            .SingleAsync();
        linhasNoOutbox.Should().Be(0);
    }
}
