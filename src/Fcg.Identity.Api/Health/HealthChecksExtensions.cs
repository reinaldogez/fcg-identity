using Fcg.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fcg.Identity.Api.Health;

internal static class HealthChecksExtensions
{
    internal static IServiceCollection AddIdentityHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>(tags: ["ready"]);

        // MassTransit 9.x registra o check do bus com a tag "ready" por padrão.
        // O Outbox desacopla a API do broker, então a indisponibilidade do broker
        // não deve afetar a readiness — removemos a tag "ready" de todos os checks
        // do MassTransit.
        services.PostConfigure<HealthCheckServiceOptions>(opts =>
        {
            foreach (
                HealthCheckRegistration registration in opts.Registrations.Where(r =>
                    r.Name.StartsWith("masstransit", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                registration.Tags.Remove("ready");
            }
        });

        return services;
    }

    internal static WebApplication MapIdentityHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        app.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                Predicate = c => c.Tags.Contains("ready"),
                ResponseWriter = EscreverRespostaJson,
            }
        );

        app.MapHealthChecks(
            "/health",
            new HealthCheckOptions { ResponseWriter = EscreverRespostaJson }
        );

        return app;
    }

    private static Task EscreverRespostaJson(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString() }
            ),
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}
