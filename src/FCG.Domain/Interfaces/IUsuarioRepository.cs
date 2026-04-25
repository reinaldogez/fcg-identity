using FCG.Domain.Entities;
using FCG.Domain.ValueObjects;

namespace FCG.Domain.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task<bool> ExisteComEmailAsync(Email email, CancellationToken cancellationToken = default);
}
