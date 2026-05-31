using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;

namespace Fcg.Identity.Application.UseCases.Relatorios;

public class ObterRelatorioUsuariosUseCase(IUsuarioReadRepository repositorio)
{
    public Task<RelatorioUsuariosDto> ExecutarAsync(
        CancellationToken cancellationToken = default
    ) => repositorio.ObterRelatorioAsync(cancellationToken);
}
