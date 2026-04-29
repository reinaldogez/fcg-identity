using FCG.Domain.Entities;

namespace FCG.Application.Interfaces;

public interface IJwtTokenService
{
    AccessToken GerarAccessToken(Usuario usuario);
    RefreshTokenGerado GerarRefreshToken();
    string CalcularHashRefreshToken(string plaintext);
}

public sealed record AccessToken(string Token, DateTime ExpiraEm, int ExpiresInSeconds);

public sealed record RefreshTokenGerado(string Plaintext, string Hash, DateTime ExpiraEm);
