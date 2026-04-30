using FCG.Application.DTOs;
using FCG.Domain.Entities;

namespace FCG.Application.Interfaces;

public interface IJwtTokenService
{
    AccessToken GerarAccessToken(Usuario usuario);
    RefreshTokenGerado GerarRefreshToken();
    string CalcularHashRefreshToken(string plaintext);
}
