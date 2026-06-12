using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Infrastructure.Dapper;
using Fcg.Identity.Infrastructure.Dapper.ReadRepositories;
using Fcg.Identity.Infrastructure.Messaging;
using Fcg.Identity.Infrastructure.Persistence;
using Fcg.Identity.Infrastructure.Persistence.Repositories;
using Fcg.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fcg.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection não configurada."
            );
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));

        services.AddIdentityMessaging(configuration);

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ISenhaService, SenhaService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IUsuarioReadRepository, UsuarioReadRepository>();

        services.AddHostedService<AdminSeedService>();
        if (environment.IsDevelopment())
        {
            services.AddHostedService<DevSeedService>();
        }

        return services;
    }
}
