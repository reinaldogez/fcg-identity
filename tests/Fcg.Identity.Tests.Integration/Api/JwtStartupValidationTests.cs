using Fcg.Identity.Api.Authentication;
using Fcg.Identity.Application.Options;
using FluentAssertions;

namespace Fcg.Identity.Tests.Integration.Api;

// O fail-fast roda no startup, antes de o host subir, e a configuração do Jwt é lida cedo demais para
// ser injetada via WebApplicationFactory; por isso exercitamos a mesma validação de chave diretamente.
// O caminho de sucesso é coberto por cada teste de integração que sobe a API.
public class JwtStartupValidationTests
{
    [Fact]
    public void DeveFalharQuandoRsaPrivateKeyPemVazio()
    {
        var settings = new JwtSettings
        {
            RsaPrivateKeyPem = string.Empty,
            KeyId = "fcg-identity-key-1",
        };

        Action acao = () => JwtKeyConfiguration.CriarChaveDeValidacao(settings);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeveFalharQuandoRsaPrivateKeyPemNaoImportavel()
    {
        var settings = new JwtSettings
        {
            RsaPrivateKeyPem =
                "-----BEGIN PRIVATE KEY-----\nnao-eh-uma-chave-valida\n-----END PRIVATE KEY-----",
            KeyId = "fcg-identity-key-1",
        };

        Action acao = () => JwtKeyConfiguration.CriarChaveDeValidacao(settings);

        acao.Should().Throw<InvalidOperationException>();
    }
}
