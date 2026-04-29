using FCG.Domain.Entities;

namespace FCG.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task AdicionarAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
