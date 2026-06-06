using System.Security.Cryptography;
using Fcg.Identity.Application.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fcg.Identity.Api.Authentication;

public static class JwtKeyConfiguration
{
    // Validação fail-fast da chave de assinatura e derivação da chave pública usada pelo JwtBearer.
    // Lança em configuração inválida para abortar o startup antes de o host subir.
    public static RsaSecurityKey CriarChaveDeValidacao(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.RsaPrivateKeyPem))
        {
            throw new InvalidOperationException(
                "Jwt:RsaPrivateKeyPem não configurada. Forneça a chave privada RSA em PEM (PKCS#8) via user-secrets ou variável de ambiente."
            );
        }

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(settings.RsaPrivateKeyPem);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Jwt:RsaPrivateKeyPem inválida: não foi possível importar a chave RSA do PEM informado.",
                ex
            );
        }

        return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false))
        {
            KeyId = settings.KeyId,
        };
    }
}
