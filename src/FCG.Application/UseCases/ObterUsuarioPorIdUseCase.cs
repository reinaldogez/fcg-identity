using FCG.Application.DTOs;
using FCG.Domain.Interfaces;

namespace FCG.Application.UseCases;

public class ObterUsuarioPorIdUseCase(IUsuarioRepository repositorio)
{
    public async Task<UsuarioResponse?> ExecutarAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var usuario = await repositorio.ObterPorIdAsync(id, cancellationToken);

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
