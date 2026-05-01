using System.Globalization;
using System.Net.Http.Headers;
using FCG.Application.DTOs;
using FCG.Application.Interfaces;
using FCG.Domain.Entities;
using FCG.Domain.Enums;
using FCG.Domain.ValueObjects;
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
    public const string TestSigningKey = "chave-de-teste-com-tamanho-minimo-de-32-caracteres-ok";
    public const string TestIssuer = "FcgApi.Tests";
    public const string TestAudience = "FcgClients.Tests";

    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-latest"
    ).Build();

    static FcgApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestAudience);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("Jwt__AccessTokenExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenExpirationDays", "7");
    }

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

    public async Task<(Guid Id, string Token)> CriarUsuarioAutenticadoAsync(
        string email,
        string nome = "Usuario Teste",
        string senhaPlaintextParaHash = "Senha@123",
        TipoUsuario tipo = TipoUsuario.Usuario
    )
    {
        using IServiceScope scope = Services.CreateScope();
        FcgDbContext contexto = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        ISenhaService senhaService = scope.ServiceProvider.GetRequiredService<ISenhaService>();
        IJwtTokenService jwtTokenService =
            scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        SenhaHash senhaHash = senhaService.GerarHash(senhaPlaintextParaHash);
        Usuario usuario = Usuario.Criar(nome, Email.Criar(email), senhaHash, tipo);
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
                d.ServiceType == typeof(DbContextOptions<FcgDbContext>)
            );
            services.Remove(dbContextDescriptor);

            services.AddDbContext<FcgDbContext>(options =>
                options.UseSqlServer(_sqlContainer.GetConnectionString())
            );

            ServiceDescriptor seedDescriptor = services.Single(d =>
                d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(AdminSeedService)
            );
            services.Remove(seedDescriptor);
        });
    }
}
