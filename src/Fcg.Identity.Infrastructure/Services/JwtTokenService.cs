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

public sealed class JwtTokenService : IJwtTokenService, IDisposable
{
    private readonly JwtSettings _settings;
    private readonly RSA _rsa;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
        _rsa = RSA.Create();
        _rsa.ImportFromPem(_settings.RsaPrivateKeyPem);

        // O provider de assinatura fica fora do cache estático global do Microsoft.IdentityModel:
        // como este serviço é dono da RSA e a descarta no Dispose, um provider mantido no cache
        // global sobreviveria à RSA descartada e quebraria assinaturas posteriores que reusassem
        // a mesma entrada de cache (mesma chave).
        var key = new RsaSecurityKey(_rsa)
        {
            KeyId = _settings.KeyId,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        };
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

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

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiraEm,
            signingCredentials: _signingCredentials
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

    public void Dispose() => _rsa.Dispose();
}
