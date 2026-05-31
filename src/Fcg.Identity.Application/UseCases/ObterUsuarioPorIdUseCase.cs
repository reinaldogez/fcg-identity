using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;

namespace Fcg.Identity.Application.UseCases;

public class ObterUsuarioPorIdUseCase(IUsuarioRepository repositorio)
{
    public async Task<UsuarioResponse?> ExecutarAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        Usuario? usuario = await repositorio.ObterPorIdAsync(id, cancellationToken);

        if (usuario is null)
            return null;

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
