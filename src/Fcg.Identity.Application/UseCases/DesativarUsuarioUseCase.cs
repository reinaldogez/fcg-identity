using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;

namespace Fcg.Identity.Application.UseCases;

public class DesativarUsuarioUseCase(IUsuarioRepository repositorio, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Usuario? usuario = await repositorio.ObterPorIdAsync(id, cancellationToken);
        if (usuario is null)
            return false;

        if (usuario.Ativo)
        {
            usuario.Desativar();
            repositorio.Atualizar(usuario);
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
        }

        return true;
    }
}
