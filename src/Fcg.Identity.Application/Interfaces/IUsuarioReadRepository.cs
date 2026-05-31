using Fcg.Identity.Application.DTOs;

namespace Fcg.Identity.Application.Interfaces;

public interface IUsuarioReadRepository
{
    Task<RelatorioUsuariosDto> ObterRelatorioAsync(CancellationToken cancellationToken = default);
}
