using FCG.Application.DTOs;

namespace FCG.Application.Interfaces;

public interface IUsuarioReadRepository
{
    Task<RelatorioUsuariosDto> ObterRelatorioAsync(CancellationToken cancellationToken = default);
}
