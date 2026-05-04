using FCG.Domain.Entities;
using FCG.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(FcgDbContext contexto) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> ObterPorHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    )
    {
        return await contexto.RefreshTokens.FirstOrDefaultAsync(
            rt => rt.TokenHash == tokenHash,
            cancellationToken
        );
    }

    public async Task AdicionarAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default
    ) => await contexto.RefreshTokens.AddAsync(refreshToken, cancellationToken);
}
