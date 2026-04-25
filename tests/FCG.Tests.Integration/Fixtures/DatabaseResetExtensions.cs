using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Tests.Integration.Fixtures;

public static class DatabaseResetExtensions
{
    public static async Task ResetarBancoAsync(this FcgApiFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FcgDbContext context = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Usuarios");
    }
}
