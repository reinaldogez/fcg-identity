using Fcg.Identity.Domain.Entities;

namespace Fcg.Identity.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObterPorHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );
    Task AdicionarAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
