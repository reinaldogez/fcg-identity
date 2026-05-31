using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.ValueObjects;

namespace Fcg.Identity.Domain.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task<bool> ExisteComEmailAsync(Email email, CancellationToken cancellationToken = default);
    void Atualizar(Usuario usuario);
    Task<(IReadOnlyList<Usuario> Items, int Total)> ListarPaginadoAsync(
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default
    );

    // Exposto exclusivamente para projeções de leitura (GraphQL com filtering/sorting/paging).
    IQueryable<Usuario> Query();
}
