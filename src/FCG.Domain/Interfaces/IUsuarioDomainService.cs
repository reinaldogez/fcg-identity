using FCG.Domain.Entities;
using FCG.Domain.ValueObjects;

namespace FCG.Domain.Interfaces;

public interface IUsuarioDomainService
{
    Task<Usuario> RegistrarAsync(
        string nome, Email email, SenhaHash senhaHash,
        CancellationToken cancellationToken = default);
}
