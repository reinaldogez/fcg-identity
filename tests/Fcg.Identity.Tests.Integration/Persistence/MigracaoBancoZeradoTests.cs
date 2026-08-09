using Fcg.Identity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Fcg.Identity.Tests.Integration.Persistence;

// Fora da coleção compartilhada de propósito: a fixture de integração já migrou o banco dela na
// inicialização, e o que se exercita aqui é justamente o estado anterior a isso — servidor de pé
// com o catálogo `identity` ainda inexistente, que é a condição em que a migração de deploy roda.
// Por isso a classe sobe um SQL Server próprio e descartável.
public class MigracaoBancoZeradoTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-latest"
    ).Build();

    public Task InitializeAsync() => _sqlContainer.StartAsync();

    public async Task DisposeAsync() => await _sqlContainer.DisposeAsync();

    [Fact]
    public async Task DeveMigrarBancoZeradoCriandoOCatalogo()
    {
        (await CatalogoIdentityExisteAsync())
            .Should()
            .BeFalse("o catálogo não existe antes de migrar");

        Func<Task> migrar = MigrarAsync;

        await migrar.Should().NotThrowAsync();
        (await CatalogoIdentityExisteAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task DeveSerIdempotenteQuandoMigraDuasVezes()
    {
        await MigrarAsync();

        Func<Task> segundaMigracao = MigrarAsync;

        await segundaMigracao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeveDeixarOEsquemaConsultavelDepoisDeMigrar()
    {
        await MigrarAsync();

        using IdentityDbContext contexto = CriarContexto();

        int total = await contexto.Usuarios.CountAsync();

        total.Should().Be(0);
    }

    // Reproduz o caminho de migração de deploy: contexto apontado para o catálogo `identity`, que o
    // próprio MigrateAsync cria quando ainda não existe.
    private async Task MigrarAsync()
    {
        using IdentityDbContext contexto = CriarContexto();
        await contexto.Database.MigrateAsync();
    }

    private IdentityDbContext CriarContexto()
    {
        DbContextOptions<IdentityDbContext> options =
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlServer(IdentityConnectionString())
                .Options;

        return new IdentityDbContext(options);
    }

    private async Task<bool> CatalogoIdentityExisteAsync()
    {
        using var conexao = new SqlConnection(_sqlContainer.GetConnectionString());
        await conexao.OpenAsync();

        using SqlCommand comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = 'identity'";

        return (int)(await comando.ExecuteScalarAsync())! == 1;
    }

    // O módulo Testcontainers.MsSql não cria banco próprio: GetConnectionString() aponta para o
    // catálogo `master`. Reescrevemos o Initial Catalog para `identity`, que é o alvo da migração.
    private string IdentityConnectionString() =>
        new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
        {
            InitialCatalog = "identity",
        }.ConnectionString;
}
