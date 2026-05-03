namespace FCG.Infrastructure.Dapper.Sql;

internal static class RelatorioUsuariosSql
{
    // 4 SELECTs num único round-trip via QueryMultipleAsync.
    // Tipo: 0 = Usuario, 1 = Administrador (FCG.Domain.Enums.TipoUsuario).
    public const string Query = """
        SELECT
            COUNT(*)                                 AS TotalUsuarios,
            SUM(CASE WHEN Ativo = 1 THEN 1 ELSE 0 END) AS TotalAtivos,
            SUM(CASE WHEN Ativo = 0 THEN 1 ELSE 0 END) AS TotalInativos
        FROM Usuarios;

        SELECT
            SUM(CASE WHEN Tipo = 0 THEN 1 ELSE 0 END) AS Usuario,
            SUM(CASE WHEN Tipo = 1 THEN 1 ELSE 0 END) AS Administrador
        FROM Usuarios;

        SELECT COUNT(*) AS Total
        FROM Usuarios
        WHERE DataCriacao >= DATEADD(day, -30, SYSUTCDATETIME());

        SELECT
            FORMAT(DATEFROMPARTS(YEAR(DataCriacao), MONTH(DataCriacao), 1), 'yyyy-MM') AS Mes,
            COUNT(*) AS Total
        FROM Usuarios
        WHERE DataCriacao >= DATEADD(month, -6, SYSUTCDATETIME())
        GROUP BY YEAR(DataCriacao), MONTH(DataCriacao)
        ORDER BY YEAR(DataCriacao), MONTH(DataCriacao);
        """;
}
