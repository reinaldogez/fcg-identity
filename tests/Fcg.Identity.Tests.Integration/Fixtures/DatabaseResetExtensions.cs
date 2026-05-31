using Fcg.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Identity.Tests.Integration.Fixtures;

public static class DatabaseResetExtensions
{
    public static async Task ResetarBancoAsync(this IdentityApiFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM RefreshTokens");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Usuarios");
    }
}
