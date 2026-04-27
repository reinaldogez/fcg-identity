using FCG.Infrastructure.Persistence;
using FCG.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace FCG.Tests.Integration.Fixtures;

public class FcgApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        FcgDbContext context = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:PermitLimit"] = int.MaxValue.ToString(),
                ["RateLimit:WindowInSeconds"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            ServiceDescriptor dbContextDescriptor = services.Single(d =>
                d.ServiceType == typeof(DbContextOptions<FcgDbContext>));
            services.Remove(dbContextDescriptor);

            services.AddDbContext<FcgDbContext>(options =>
                options.UseSqlServer(_sqlContainer.GetConnectionString()));

            ServiceDescriptor seedDescriptor = services.Single(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(AdminSeedService));
            services.Remove(seedDescriptor);
        });
    }
}
