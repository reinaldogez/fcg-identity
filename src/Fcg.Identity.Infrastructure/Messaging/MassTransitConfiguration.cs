using Fcg.Contracts.Events;
using Fcg.Identity.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Identity.Infrastructure.Messaging;

// Config do bus MassTransit + EF Outbox. O nome da exchange `user-created` é cravado AQUI via message
// topology (SetEntityName), não no contrato — o record permanece puro, sem dependência de transporte.
// AddMassTransit só registra serviços no container; nada conecta ao broker durante o Build. O
// IBusControl inicia como IHostedService depois, e o UseBusOutbox desacopla o publish do broker.
public static class MassTransitConfiguration
{
    public static IServiceCollection AddIdentityMessaging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<IdentityDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });

            x.UsingRabbitMq(
                (context, cfg) =>
                {
                    // Host vem de config: env var em ambiente de teste (porta dinâmica do
                    // Testcontainer), ConfigMap+Secret em produção, user-secrets/.env em dev local.
                    // Fail-fast no mesmo espírito de DefaultConnection/Jwt.
                    string uri =
                        configuration["RabbitMq:Uri"]
                        ?? throw new InvalidOperationException("RabbitMq:Uri não configurada.");

                    cfg.Host(new Uri(uri));

                    cfg.Message<UserCreatedEvent>(m => m.SetEntityName("user-created"));
                    cfg.Publish<UserCreatedEvent>(p => p.ExchangeType = "fanout");

                    cfg.ConfigureEndpoints(context);
                }
            );
        });

        return services;
    }
}
