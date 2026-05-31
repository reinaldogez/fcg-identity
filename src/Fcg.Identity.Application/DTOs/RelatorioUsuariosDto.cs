namespace Fcg.Identity.Application.DTOs;

public record RelatorioUsuariosDto(
    int TotalUsuarios,
    int TotalAtivos,
    int TotalInativos,
    TotalPorTipoDto PorTipo,
    int CadastrosUltimos30Dias,
    IReadOnlyList<CadastroPorMesDto> CadastrosPorMes
);
