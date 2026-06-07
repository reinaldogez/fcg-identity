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
    public const string TestIssuer = "FcgApi.Tests";
    public const string TestAudience = "FcgClients.Tests";
    public const string TestKeyId = "fcg-identity-key-1";

    // Chave RSA descartável, exclusiva da suíte de teste (gerada localmente, sem relação com a
    // chave de qualquer ambiente). O processo da API a lê via Jwt__RsaPrivateKeyPem para assinar
    // RS256; os testes derivam a chave pública correspondente para validar os tokens emitidos.
    public const string TestRsaPrivateKeyPem = """
        -----BEGIN PRIVATE KEY-----
        MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDiQBueh8Ed/D0K
        +kYx+Ak+5otwizeVDEmPq1+tvHBZqCjxZONbk7XUfaoegTt7kEzv9xf2noHCri9o
        8K3i4q2sC5VvKSn8my/5qRqHzsD8gwIWx/0EApOMa/Xt3+/mMIHryolzTY8u/rjP
        grBXI0D9ALCtAmaDTApQAAmfWFFqSEanI8sEAJxpAkr7vKsycs2BQNGDRsW0SdYr
        jmWlEF9TJo5avalSAjYe58IzcKCLi0lPFgPFMWbm4gpQLsKFqofgOgMoaqWDC7tu
        hXY9/lxOVlhR65VMNsN9kEtPl/ZYLa8zOnrdSJGGOweNK0xjjrd5a/KWNgHZxHFq
        uHyufJfNAgMBAAECggEADLbzlmYkqS26tHo6Jaa9xkYogeug9QRawfMsjlPvsGot
        2tsDl+rmJgnl3I8Aq8IBQN8O/rILssgdK/WSoBSDFA8Wl8elb2e9O3eQYR9yYv5t
        yJ/2jRoj9pk+md6i2bnSI1EfhlZOfKKd+jNq+4qkpVM7mo1u+2PzlGlcIRNSh/lq
        ydGvvECjJxvMQTGl25H1mZf2DJVJuQq3PI3shhGK3frLtnoFMECil4YZ0a46KrU1
        v7nt6jJbmsIKaIliFxFhI6lD1oz9Vj3qZ73cHQ32A9jILtW020HASEWP2NGjDwo1
        LJhSQsRpUXUvWFLS3QvdwjqS0Mk7vLOC1ym2ToFA4QKBgQDzZNRKjwYUiVzzJz2i
        bUYDIxd0QT8ZGIQuHZCBkIEdH5kp/2CfF0tkoyFuo2TyhaLsdC8z4jCJvwYt4pRG
        j8Nh1hT4M9X0k9OWke7d55Aobo+U8jL/Ummad3GJP7+Is/6/pGkQGHNi8QNufCfL
        rId648Fue+nyCW+ZjF1VAjSB6QKBgQDt9/kQadTUNi/8xkD1o+Sg+MdzmhLp0pV3
        ujkJi8Mdz+aF+ud0a2HjwNFsiAyGRMlmUAu+/MWoCwmWC5xjC46Uji7PDUSx0mzb
        Iw0vzX5S42esZKnw5UEQI2TiSZLKCmlydMmSl1PSE3qv+AxjCzjPT4JEejcsP1G/
        SQNnDLh0RQKBgGm5vs20WvvIv2uP/CH2PZdXQvTo8rPABorRpNfjIXK5KxsnJ51z
        zPgmNHuO1mbSzfbQcUCkXFk5dUGxTp9oC4MQL4OxYJshK6QYOB6EXAZ0IEKfArAN
        6HmEsPjhjB2hsmMk085+EIFGGCuCGvdKNn+XN4r6oKDWoHeelVw73PshAoGBANmp
        7LX3p4Vn/yK9kGNewtv+UilKL6ySQscdndg+b30QUfIQ2q6hHgu9rZERLCuQNYuR
        Af1ypbScS+tjuWrbAlKdbvFSWJgyOgGDISetVbOpb4W/GbZPa+DADyHwXATT2zmm
        201rf27zBFB6mZHqjM8LEcNi6p5dWH+X4DXc68blAoGBAI+ZL/JxyoxVVCO7ylo+
        JcJ41qgAlmbNiM8xbApaB81Zosvd6RMIhcYuZmYtCpGtqh3768zcGkDd1jZMiU1y
        E9qEP703h6jcauYPOroi9vjOXPnR6GZTbtKmBZTV0rTkGl8Gnu4l2Pu3nP3xcBKL
        LyEVyIo8XWSOtGPC9LHQBxtb
        -----END PRIVATE KEY-----
        """;

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
        Environment.SetEnvironmentVariable("Jwt__RsaPrivateKeyPem", TestRsaPrivateKeyPem);
        Environment.SetEnvironmentVariable("Jwt__KeyId", TestKeyId);
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

    // Derruba o broker para simular o RabbitMQ fora do ar depois que a API já subiu. Use só em
    // classes cujos testes não dependem do broker (ex.: health checks) — a instância da factory é
    // exclusiva da classe de teste, então parar o broker aqui não afeta as demais.
    public Task PararBrokerAsync() => _rabbitMqContainer.StopAsync();

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
