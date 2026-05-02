using FCG.Application.DTOs;
using FCG.Application.Interfaces;
using FCG.Domain.Entities;
using FCG.Domain.Exceptions;
using FCG.Domain.Interfaces;

namespace FCG.Application.UseCases;

public class RefreshTokenUseCase(
    IUsuarioRepository usuarioRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork
)
{
    private const string MensagemFalha = "Refresh token inválido.";

    public async Task<LoginResponse> ExecutarAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default
    )
    {
        string hash = jwtTokenService.CalcularHashRefreshToken(request.RefreshToken);

        RefreshToken? tokenExistente = await refreshTokenRepository.ObterPorHashAsync(
            hash,
            cancellationToken
        );
        if (tokenExistente is null || !tokenExistente.EstaAtivo)
        {
            throw new DomainAuthException(MensagemFalha);
        }

        Usuario? usuario = await usuarioRepository.ObterPorIdAsync(
            tokenExistente.UsuarioId,
            cancellationToken
        );
        if (usuario is null || !usuario.Ativo)
        {
            throw new DomainAuthException(MensagemFalha);
        }

        AccessToken accessToken = jwtTokenService.GerarAccessToken(usuario);
        RefreshTokenGerado novoRefresh = jwtTokenService.GerarRefreshToken();
        var novoToken = RefreshToken.Criar(usuario.Id, novoRefresh.Hash, novoRefresh.ExpiraEm);

        tokenExistente.RevogarESubstituirPor(novoToken.Id);
        await refreshTokenRepository.AdicionarAsync(novoToken, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return new LoginResponse(
            accessToken.Token,
            "Bearer",
            accessToken.ExpiresInSeconds,
            novoRefresh.Plaintext
        );
    }
}
