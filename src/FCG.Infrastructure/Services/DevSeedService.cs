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

public class DevSeedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DevSeedService> logger
) : IHostedService
{
    private const int QuantidadeAlvo = 50;
    private const string SenhaPadrao = "Senha@123";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? enabled = configuration["DevSeed:Enabled"];
        if (!bool.TryParse(enabled, out bool ativado) || !ativado)
        {
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        FcgDbContext contexto = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        ISenhaService senhaService = scope.ServiceProvider.GetRequiredService<ISenhaService>();

        int totalAtual = await contexto.Usuarios.CountAsync(cancellationToken);
        if (totalAtual >= QuantidadeAlvo)
        {
#pragma warning disable CA1873 // log diagnostico, argumentos triviais
            logger.LogInformation(
                "DevSeed ignorado — banco já tem {Total} usuários (alvo: {Alvo}).",
                totalAtual,
                QuantidadeAlvo
            );
#pragma warning restore CA1873
            return;
        }

        SenhaHash senhaHash = senhaService.GerarHash(SenhaPadrao);
#pragma warning disable CA5394 // seed de desenvolvimento, randomness nao precisa ser criptografica
        var random = new Random(42);
        DateTime hoje = DateTime.UtcNow;

        int aCriar = QuantidadeAlvo - totalAtual;
        for (int i = 0; i < aCriar; i++)
        {
            int sequencial = totalAtual + i + 1;
            string emailEndereco = $"dev.user{sequencial:D3}@fcg.local";

            // Distribui DataCriacao nos últimos 180 dias (~6 meses) para alimentar
            // tanto o "últimos 30 dias" quanto a série mensal do relatório.
            int diasAtras = random.Next(0, 180);
            DateTime dataCriacao = hoje.AddDays(-diasAtras);

            TipoUsuario tipo = i % 10 == 0 ? TipoUsuario.Administrador : TipoUsuario.Usuario;
            bool ativo = i % 7 != 0;

            var usuario = Usuario.Criar(
                $"Dev User {sequencial:D3}",
                Email.Criar(emailEndereco),
                senhaHash,
                tipo
            );

            // Reflexão controlada para sobrescrever DataCriacao, que é private set por design
            // no domínio. Aceitável neste serviço de seed de desenvolvimento.
            typeof(Usuario)
                .GetProperty(nameof(Usuario.DataCriacao))!
                .SetValue(usuario, dataCriacao);
            if (!ativo)
            {
                usuario.Desativar();
            }

            await contexto.Usuarios.AddAsync(usuario, cancellationToken);
        }
#pragma warning restore CA5394

        await contexto.SaveChangesAsync(cancellationToken);
#pragma warning disable CA1873 // log diagnostico, argumentos triviais
        logger.LogInformation(
            "DevSeed concluído — {Criados} usuários criados (total agora: {Total}).",
            aCriar,
            totalAtual + aCriar
        );
#pragma warning restore CA1873
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
