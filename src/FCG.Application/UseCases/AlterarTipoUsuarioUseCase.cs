using FCG.Application.DTOs;
using FCG.Domain.Interfaces;

namespace FCG.Application.UseCases;

public class AlterarTipoUsuarioUseCase(IUsuarioRepository repositorio, IUnitOfWork unitOfWork)
{
    public async Task<UsuarioResponse?> ExecutarAsync(
        Guid id,
        Guid solicitanteId,
        AlterarTipoRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var usuario = await repositorio.ObterPorIdAsync(id, cancellationToken);
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
