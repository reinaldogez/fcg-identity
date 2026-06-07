using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Fcg.Identity.Api.Authentication;
using Fcg.Identity.Api.Authorization;
using Fcg.Identity.Api.GraphQL;
using Fcg.Identity.Api.Health;
using Fcg.Identity.Api.Logging;
using Fcg.Identity.Api.Middlewares;
using Fcg.Identity.Api.OpenApi;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Application.Options;
using Fcg.Identity.Application.UseCases;
using Fcg.Identity.Application.UseCases.Relatorios;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.Services;
using Fcg.Identity.Infrastructure.Dapper;
using Fcg.Identity.Infrastructure.Dapper.ReadRepositories;
using Fcg.Identity.Infrastructure.Messaging;
using Fcg.Identity.Infrastructure.Persistence;
using Fcg.Identity.Infrastructure.Persistence.Repositories;
using Fcg.Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(
    (services, lc) =>
        lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "Fcg.Identity.Api")
            .Enrich.With<ActivityEnricher>()
);

builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "Fcg.Identity.Api", serviceVersion: "1.0.0"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddConsoleExporter());

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

string connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddIdentityMessaging(builder.Configuration);

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ISenhaService, SenhaService>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUsuarioDomainService, UsuarioDomainService>();
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IUsuarioReadRepository, UsuarioReadRepository>();
builder.Services.AddScoped<CadastrarUsuarioUseCase>();
builder.Services.AddScoped<ObterUsuarioPorIdUseCase>();
builder.Services.AddScoped<ListarUsuariosUseCase>();
builder.Services.AddScoped<AtualizarUsuarioUseCase>();
builder.Services.AddScoped<AlterarSenhaUseCase>();
builder.Services.AddScoped<DesativarUsuarioUseCase>();
builder.Services.AddScoped<AtivarUsuarioUseCase>();
builder.Services.AddScoped<AlterarTipoUsuarioUseCase>();
builder.Services.AddScoped<LoginUseCase>();
builder.Services.AddScoped<RefreshTokenUseCase>();
builder.Services.AddScoped<LogoutUseCase>();
builder.Services.AddScoped<ObterRelatorioUsuariosUseCase>();
builder.Services.AddHostedService<AdminSeedService>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<DevSeedService>();
}

builder.Services.AddFcgGraphQL();
builder.Services.AddIdentityHealthChecks();

WebApplication app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSerilogRequestLogging();

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

await app.RunAsync();
