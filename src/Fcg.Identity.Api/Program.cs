using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Fcg.Identity.Api.Authentication;
using Fcg.Identity.Api.Authorization;
using Fcg.Identity.Api.GraphQL;
using Fcg.Identity.Api.Health;
using Fcg.Identity.Api.Jwks;
using Fcg.Identity.Api.Logging;
using Fcg.Identity.Api.Middlewares;
using Fcg.Identity.Api.OpenApi;
using Fcg.Identity.Application;
using Fcg.Identity.Application.Options;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.Services;
using Fcg.Identity.Infrastructure;
using Fcg.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Sinks/exporters de rede (Loki, OTLP) só entram no pipeline quando o endpoint está
// configurado (ConfigMap no k8s); sem endpoint a app sobe sem tentativa alguma de conexão.
string? lokiUrl = builder.Configuration["Loki:Url"];
bool otlpHabilitado = !string.IsNullOrWhiteSpace(
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
);

builder.Services.AddSerilog(
    (services, lc) =>
    {
        lc.ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "Fcg.Identity.Api")
            .Enrich.With<ActivityEnricher>();

        if (!string.IsNullOrWhiteSpace(lokiUrl))
        {
            lc.WriteTo.GrafanaLoki(
                lokiUrl,
                labels: [new LokiLabel { Key = "app", Value = "fcg-identity" }]
            );
        }
    }
);

builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "Fcg.Identity.Api", serviceVersion: "1.0.0"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation().AddSource("MassTransit").AddConsoleExporter();
        if (otlpHabilitado)
        {
            t.AddOtlpExporter();
        }
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation().AddMeter("MassTransit");
        if (otlpHabilitado)
        {
            m.AddOtlpExporter();
        }
    });

builder
    .Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<AuthorizeOperationTransformer>();
});

IConfigurationSection rateLimitConfig = builder.Configuration.GetSection("RateLimit");
builder.Services.AddRateLimiter(options =>
{
    int permitLimit = rateLimitConfig.GetValue<int>("PermitLimit");
    var window = TimeSpan.FromSeconds(rateLimitConfig.GetValue<int>("WindowInSeconds"));
    options.AddPolicy(
        "fixed",
        httpContext =>
        {
            string? identity =
                httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? httpContext.User.Identity?.Name;

            if (identity != null)
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    identity,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0,
                    }
                );
            }

            string remoteIp =
                httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(
                remoteIp,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                }
            );
        }
    );
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

JwtSettings jwtSettings =
    builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Seção 'Jwt' não configurada.");

RsaSecurityKey chaveValidacao = JwtKeyConfiguration.CriarChaveDeValidacao(jwtSettings);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = chaveValidacao,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = JwtRegisteredClaimNames.Sub,
        };
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationHandler, OwnerOrAdminHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "OwnerOrAdmin",
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new OwnerOrAdminRequirement(routeParameterName: "id"));
        }
    );
});

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApplication();
builder.Services.AddScoped<IUsuarioDomainService, UsuarioDomainService>();

builder.Services.AddFcgGraphQL();
builder.Services.AddIdentityHealthChecks();

WebApplication app = builder.Build();

// Migração é ato explícito: o Job dedicado reusa esta imagem com --migrate, aplica as
// migrations pendentes e encerra sem subir o host web. O boot normal não migra.
if (args.Contains("--migrate"))
{
    using IServiceScope migrationScope = app.Services.CreateScope();
    await migrationScope
        .ServiceProvider.GetRequiredService<IdentityDbContext>()
        .Database.MigrateAsync();
    return;
}

app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "FCG API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapGraphQL("/graphql");
app.MapIdentityHealthChecks();
app.MapJwksEndpoint();

await app.RunAsync();
