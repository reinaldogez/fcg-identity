using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Domain.Entities;

namespace Fcg.Identity.Application.Interfaces;

public interface IJwtTokenService
{
    AccessToken GerarAccessToken(Usuario usuario);
    RefreshTokenGerado GerarRefreshToken();
    string CalcularHashRefreshToken(string plaintext);
}
