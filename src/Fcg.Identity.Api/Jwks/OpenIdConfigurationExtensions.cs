using Fcg.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace Fcg.Identity.Api.Jwks;

internal static class OpenIdConfigurationExtensions
{
    private static readonly string[] _responseTypesSupported = ["id_token"];
    private static readonly string[] _subjectTypesSupported = ["public"];
    private static readonly string[] _idTokenSigningAlgValuesSupported = ["RS256"];

    internal static WebApplication MapOpenIdConfigurationEndpoint(this WebApplication app)
    {
        // Diferente do JWKS, as configurações são resolvidas por request: sem base pública
        // configurada o documento depende do host que atendeu a chamada.
        app.MapGet(
                "/.well-known/openid-configuration",
                (HttpContext contexto, IOptions<JwtSettings> options) =>
                {
                    JwtSettings settings = options.Value;

                    // Atrás de um gateway, o jwks_uri anunciado precisa apontar para a base pública,
                    // não para o host interno que atendeu a request.
                    string baseUrl = string.IsNullOrWhiteSpace(settings.PublicBaseUrl)
                        ? $"{contexto.Request.Scheme}://{contexto.Request.Host}"
                        : settings.PublicBaseUrl.TrimEnd('/');

                    // Nomes de campo do protocolo OIDC: snake_case, não traduzidos.
                    var documento = new
                    {
                        issuer = settings.Issuer,
                        jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                        response_types_supported = _responseTypesSupported,
                        subject_types_supported = _subjectTypesSupported,
                        id_token_signing_alg_values_supported = _idTokenSigningAlgValuesSupported,
                    };

                    return Results.Json(documento);
                }
            )
            .AllowAnonymous();

        return app;
    }
}
