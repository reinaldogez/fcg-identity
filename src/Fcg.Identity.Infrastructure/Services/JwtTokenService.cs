using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Application.Options;
using Fcg.Identity.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fcg.Identity.Infrastructure.Services;

public class JwtTokenService(IOptions<JwtSettings> settings) : IJwtTokenService
{
    private readonly JwtSettings _settings = settings.Value;

    public AccessToken GerarAccessToken(Usuario usuario)
    {
        DateTime expiraEm = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email.Endereco),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, usuario.Tipo.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiraEm,
            signingCredentials: credentials
        );

        string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        int expiresInSeconds = _settings.AccessTokenExpirationMinutes * 60;

        return new AccessToken(tokenString, expiraEm, expiresInSeconds);
    }

    public RefreshTokenGerado GerarRefreshToken()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        string plaintext = Base64UrlEncoder.Encode(bytes);
        string hash = CalcularHashRefreshToken(plaintext);
        DateTime expiraEm = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays);
        return new RefreshTokenGerado(plaintext, hash, expiraEm);
    }

    public string CalcularHashRefreshToken(string plaintext)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(hashBytes);
    }
}
