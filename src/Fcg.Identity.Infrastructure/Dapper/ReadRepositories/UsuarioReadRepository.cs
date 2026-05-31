using System.Data;
using Dapper;
using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Infrastructure.Dapper.Sql;

namespace Fcg.Identity.Infrastructure.Dapper.ReadRepositories;

public class UsuarioReadRepository(IDbConnectionFactory connectionFactory) : IUsuarioReadRepository
{
    public async Task<RelatorioUsuariosDto> ObterRelatorioAsync(
        CancellationToken cancellationToken = default
    )
    {
        using IDbConnection connection = connectionFactory.CreateOpenConnection();

        CommandDefinition command = new(
            RelatorioUsuariosSql.Query,
            cancellationToken: cancellationToken
        );

        using SqlMapper.GridReader gridReader = await connection.QueryMultipleAsync(command);

        TotaisRow totais = await gridReader.ReadFirstAsync<TotaisRow>();
        TotalPorTipoDto porTipo = await gridReader.ReadFirstAsync<TotalPorTipoDto>();
        int cadastrosUltimos30Dias = await gridReader.ReadFirstAsync<int>();
        IReadOnlyList<CadastroPorMesDto> cadastrosPorMes = (
            await gridReader.ReadAsync<CadastroPorMesDto>()
        ).AsList();

        return new RelatorioUsuariosDto(
            totais.TotalUsuarios,
            totais.TotalAtivos,
            totais.TotalInativos,
            porTipo,
            cadastrosUltimos30Dias,
            cadastrosPorMes
        );
    }

    private sealed record TotaisRow(int TotalUsuarios, int TotalAtivos, int TotalInativos);
}
