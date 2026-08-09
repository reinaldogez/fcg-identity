namespace Fcg.Identity.Application.Options;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string RsaPrivateKeyPem { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;

    // Base pública usada no documento de discovery (jwks_uri). Vazio = deriva da própria request,
    // preservando o comportamento atual quando não configurado.
#pragma warning disable CA1056 // string, e não Uri: o valor vem da configuração e o vazio é o sinal de "não configurado".
    public string PublicBaseUrl { get; set; } = string.Empty;
#pragma warning restore CA1056
}
