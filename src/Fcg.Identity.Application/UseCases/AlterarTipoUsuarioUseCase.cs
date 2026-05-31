using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;

namespace Fcg.Identity.Application.UseCases;

public class AlterarTipoUsuarioUseCase(IUsuarioRepository repositorio, IUnitOfWork unitOfWork)
{
    public async Task<UsuarioResponse?> ExecutarAsync(
        Guid id,
        Guid solicitanteId,
        AlterarTipoRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Usuario? usuario = await repositorio.ObterPorIdAsync(id, cancellationToken);
        if (usuario is null)
            return null;

        usuario.AlterarTipoSolicitadoPor(request.Tipo, solicitanteId);
        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return new UsuarioResponse(
            usuario.Id,
            usuario.Nome,
            usuario.Email.Endereco,
            usuario.Tipo.ToString(),
            usuario.DataCriacao,
            usuario.Ativo
        );
    }
}
