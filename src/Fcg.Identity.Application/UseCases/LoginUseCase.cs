using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Exceptions;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.ValueObjects;

namespace Fcg.Identity.Application.UseCases;

public class LoginUseCase(
    IUsuarioRepository usuarioRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISenhaService senhaService,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork
)
{
    private const string MensagemFalha = "Credenciais inválidas.";

    public async Task<LoginResponse> ExecutarAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Email email;
        try
        {
            email = Email.Criar(request.Email);
        }
        catch (DomainException)
        {
            // Converte falha de validação em falha de auth genérica para não vazar
            // se o erro foi formato do e-mail vs. credenciais incorretas.
            throw new DomainAuthException(MensagemFalha);
        }

        Usuario usuario =
            await usuarioRepository.ObterPorEmailAsync(email, cancellationToken)
            ?? throw new DomainAuthException(MensagemFalha);

        if (!usuario.Ativo)
        {
            throw new DomainAuthException(MensagemFalha);
        }

        if (!senhaService.VerificarSenha(request.Senha, usuario.SenhaHash))
        {
            throw new DomainAuthException(MensagemFalha);
        }

        AccessToken accessToken = jwtTokenService.GerarAccessToken(usuario);
        RefreshTokenGerado refreshGerado = jwtTokenService.GerarRefreshToken();
        var refreshToken = RefreshToken.Criar(
            usuario.Id,
            refreshGerado.Hash,
            refreshGerado.ExpiraEm
        );

        await refreshTokenRepository.AdicionarAsync(refreshToken, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return new LoginResponse(
            accessToken.Token,
            "Bearer",
            accessToken.ExpiresInSeconds,
            refreshGerado.Plaintext
        );
    }
}
