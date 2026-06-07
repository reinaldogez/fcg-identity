using Fcg.Identity.Api.Authentication;
using Fcg.Identity.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fcg.Identity.Api.Jwks;

internal static class JwksExtensions
{
    internal static WebApplication MapJwksEndpoint(this WebApplication app)
    {
        JwtSettings settings = app.Services.GetRequiredService<IOptions<JwtSettings>>().Value;

        // Mesma chave pública usada na validação local; o kid acompanha o do header do token.
        RsaSecurityKey chavePublica = JwtKeyConfiguration.CriarChaveDeValidacao(settings);
        JsonWebKey jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(chavePublica);

        // Projeção restrita: apenas material público sai. d/p/q/dp/dq/qi nunca são serializados.
        var documento = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = settings.KeyId,
                    n = jwk.N,
                    e = jwk.E,
                },
            },
        };

        app.MapGet("/.well-known/jwks.json", () => Results.Json(documento)).AllowAnonymous();

        return app;
    }
}
