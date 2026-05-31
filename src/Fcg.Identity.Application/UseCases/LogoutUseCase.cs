using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;

namespace Fcg.Identity.Application.UseCases;

public class LogoutUseCase(
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork
)
{
    public async Task ExecutarAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default
    )
    {
        string hash = jwtTokenService.CalcularHashRefreshToken(request.RefreshToken);
        RefreshToken? token = await refreshTokenRepository.ObterPorHashAsync(
            hash,
            cancellationToken
        );

        if (token is null || token.RevogadoEm is not null)
        {
            return;
        }

        token.Revogar();
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }
}
