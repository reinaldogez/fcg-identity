using System.Data;
using FCG.Application.Interfaces;
using FCG.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Dapper;

// Extrai a connection string a partir do DbContext do EF.
// Manter uma única fonte de verdade evita drift entre EF e Dapper, e em testes
// (FcgApiFactory) o DbContext já é reconfigurado para o Testcontainer — Dapper acompanha automaticamente.
public class SqlConnectionFactory(FcgDbContext contexto) : IDbConnectionFactory
{
    private readonly string _connectionString =
        contexto.Database.GetConnectionString()
        ?? throw new InvalidOperationException(
            "Não foi possível resolver a connection string a partir do FcgDbContext."
        );

    public IDbConnection CreateOpenConnection()
    {
        SqlConnection connection = new(_connectionString);
        connection.Open();
        return connection;
    }
}
