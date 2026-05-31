using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;

namespace Fcg.Identity.Application.UseCases;

public class ListarUsuariosUseCase(IUsuarioRepository repositorio)
{
    public async Task<ListarUsuariosResponse> ExecutarAsync(
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default
    )
    {
        (IReadOnlyList<Usuario> items, int total) = await repositorio.ListarPaginadoAsync(
            pagina,
            tamanhoPagina,
            cancellationToken
        );

        var responses = items
            .Select(u => new UsuarioResponse(
                u.Id,
                u.Nome,
                u.Email.Endereco,
                u.Tipo.ToString(),
                u.DataCriacao,
                u.Ativo
            ))
            .ToList();

        return new ListarUsuariosResponse(responses, total, pagina, tamanhoPagina);
    }
}
