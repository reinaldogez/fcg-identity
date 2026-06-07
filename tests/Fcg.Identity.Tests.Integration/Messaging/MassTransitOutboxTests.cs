using Fcg.Contracts.Events;
using Fcg.Identity.Infrastructure.Persistence;
using Fcg.Identity.Tests.Integration.Fixtures;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Identity.Tests.Integration.Messaging;

// Prova que o bus está registrado no DI e o Outbox grava, sem acionar o fluxo de cadastro de
// usuário e sem depender do pacote de contratos compartilhado.
[Collection("Integration")]
public class MassTransitOutboxTests(IdentityApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void BusDeveEstarResolvivelNoContainerSemBrokerVivoNoBoot()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        IPublishEndpoint? publishEndpoint = scope.ServiceProvider.GetService<IPublishEndpoint>();
        IBus? bus = scope.ServiceProvider.GetService<IBus>();

        publishEndpoint.Should().NotBeNull();
        bus.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishDeUserCreatedEventDeveGravarLinhaNoOutbox()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext contexto = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        IPublishEndpoint publishEndpoint =
            scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        await publishEndpoint.Publish(
            new UserCreatedEvent
            {
                EventVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                UserId = Guid.NewGuid(),
                Name = "Usuario Teste",
                Email = "teste@exemplo.com",
            }
        );
        await contexto.SaveChangesAsync();

        // UseBusOutbox intercepta o Publish e grava na OutboxMessage no mesmo commit (entrega ao
        // broker é background pós-commit). Basta confirmar que a linha foi gravada.
        long linhasNoOutbox = await contexto
            .Database.SqlQueryRaw<long>("SELECT COUNT_BIG(*) AS Value FROM OutboxMessage")
            .SingleAsync();

        linhasNoOutbox.Should().BeGreaterThan(0);
    }
}
