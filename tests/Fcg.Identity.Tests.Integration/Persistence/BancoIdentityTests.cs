using Fcg.Identity.Infrastructure.Persistence;
using Fcg.Identity.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Identity.Tests.Integration.Persistence;

[Collection("Integration")]
public class BancoIdentityTests(IdentityApiFactory factory)
{
    [Fact]
    public async Task BancoUsadoNosTestesDeveChamarSeIdentity()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext contexto = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        string nomeBanco = await contexto
            .Database.SqlQueryRaw<string>("SELECT DB_NAME() AS Value")
            .SingleAsync();

        nomeBanco.Should().Be("identity");
    }
}
