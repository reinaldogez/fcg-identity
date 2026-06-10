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
                    // Conexão vem de config em campos separados: endereço (Host/Port) não-sensível
                    // via ConfigMap; credencial (Username/Password) via Secret. Em teste vêm da config
                    // in-memory (porta dinâmica do Testcontainer); em dev local de user-secrets/.env.
                    // Fail-fast no mesmo espírito de DefaultConnection/Jwt.
                    string host =
                        configuration["RabbitMq:Host"]
                        ?? throw new InvalidOperationException("RabbitMq:Host não configurado.");
                    string username =
                        configuration["RabbitMq:Username"]
                        ?? throw new InvalidOperationException("RabbitMq:Username não configurado.");
                    string password =
                        configuration["RabbitMq:Password"]
                        ?? throw new InvalidOperationException("RabbitMq:Password não configurado.");
                    ushort port = ushort.TryParse(
                        configuration["RabbitMq:Port"],
                        out ushort parsedPort
                    )
                        ? parsedPort
                        : (ushort)5672;

                    cfg.Host(
                        host,
                        port,
                        "/",
                        h =>
                        {
                            h.Username(username);
                            h.Password(password);
                        }
                    );

                    cfg.Message<UserCreatedEvent>(m => m.SetEntityName("user-created"));
                    cfg.Publish<UserCreatedEvent>(p => p.ExchangeType = "fanout");

                    cfg.ConfigureEndpoints(context);
                }
            );
        });

        return services;
    }
}
