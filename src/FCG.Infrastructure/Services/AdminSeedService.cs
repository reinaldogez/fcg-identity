using FCG.Application.Interfaces;
using FCG.Domain.Entities;
using FCG.Domain.Enums;
using FCG.Domain.ValueObjects;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FCG.Infrastructure.Services;

public class AdminSeedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AdminSeedService> logger
) : IHostedService
{
    private const string AdminEmail = "admin@fcg.com";
    private const string AdminNome = "Administrador";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var senha = configuration["AdminSeed:DefaultPassword"];

        if (string.IsNullOrWhiteSpace(senha))
        {
            logger.LogWarning(
                "AdminSeed:DefaultPassword não configurada. Seed do administrador ignorado."
            );
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var senhaService = scope.ServiceProvider.GetRequiredService<ISenhaService>();

        var email = Email.Criar(AdminEmail);
        bool adminExiste = await contexto.Usuarios.AnyAsync(
            u => u.Email == email,
            cancellationToken
        );

        if (adminExiste)
        {
            logger.LogInformation("Administrador já existe. Seed ignorado.");
            return;
        }

        SenhaHash senhaHash = senhaService.GerarHash(senha);
        var admin = Usuario.Criar(AdminNome, email, senhaHash, TipoUsuario.Administrador);

        await contexto.Usuarios.AddAsync(admin, cancellationToken);
        await contexto.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Administrador criado com sucesso via seed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
