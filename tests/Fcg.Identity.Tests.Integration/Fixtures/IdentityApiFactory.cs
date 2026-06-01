using System.Globalization;
using System.Net.Http.Headers;
using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Enums;
using Fcg.Identity.Domain.ValueObjects;
using Fcg.Identity.Infrastructure.Persistence;
using Fcg.Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Fcg.Identity.Tests.Integration.Fixtures;

public class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestSigningKey = "chave-de-teste-com-tamanho-minimo-de-32-caracteres-ok";
    public const string TestIssuer = "FcgApi.Tests";
    public const string TestAudience = "FcgClients.Tests";

    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-latest"
    ).Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder(
        "rabbitmq:4-management"
    ).Build();

    static IdentityApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestAudience);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("Jwt__AccessTokenExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenExpirationDays", "7");
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        // O bus lê o host RabbitMQ de RabbitMq:Uri. A porta do Testcontainer é dinâmica, então só
        // conhecemos a URI após StartAsync. Setamos a env var ANTES do primeiro acesso a `Services`
        // (o MigrateAsync abaixo força o CreateBuilder), mantendo o padrão de configuração-antes-do-
        // boot já usado para o Jwt.
        Environment.SetEnvironmentVariable(
            "RabbitMq__Uri",
            _rabbitMqContainer.GetConnectionString()
        );

        using IServiceScope scope = Services.CreateScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync();
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task<(Guid Id, string Token)> CriarUsuarioAutenticadoAsync(
        string email,
        string nome = "Usuario Teste",
        string senhaPlaintextParaHash = "Senha@123",
        TipoUsuario tipo = TipoUsuario.Usuario
    )
    {
        using IServiceScope scope = Services.CreateScope();
        IdentityDbContext contexto = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        ISenhaService senhaService = scope.ServiceProvider.GetRequiredService<ISenhaService>();
        IJwtTokenService jwtTokenService =
            scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        SenhaHash senhaHash = senhaService.GerarHash(senhaPlaintextParaHash);
        var usuario = Usuario.Criar(nome, Email.Criar(email), senhaHash, tipo);
        await contexto.Usuarios.AddAsync(usuario);
        await contexto.SaveChangesAsync();

        AccessToken accessToken = jwtTokenService.GerarAccessToken(usuario);
        return (usuario.Id, accessToken.Token);
    }

    public HttpClient CreateAuthenticatedClient(string token)
    {
        HttpClient client = CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RateLimit:PermitLimit"] = int.MaxValue.ToString(
                            CultureInfo.InvariantCulture
                        ),
                        ["RateLimit:WindowInSeconds"] = "60",
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            ServiceDescriptor dbContextDescriptor = services.Single(d =>
                d.ServiceType == typeof(DbContextOptions<IdentityDbContext>)
            );
            services.Remove(dbContextDescriptor);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseSqlServer(IdentityConnectionString())
            );

            ServiceDescriptor seedDescriptor = services.Single(d =>
                d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(AdminSeedService)
            );
            services.Remove(seedDescriptor);
        });
    }

    // O módulo Testcontainers.MsSql não cria banco próprio: GetConnectionString() aponta
    // para o catálogo `master`. Reescrevemos o Initial Catalog para `identity`
    // — o MigrateAsync cria e migra esse banco no container.
    private string IdentityConnectionString() =>
        new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
        {
            InitialCatalog = "identity",
        }.ConnectionString;
}
