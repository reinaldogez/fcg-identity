using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FCG.Application.Interfaces;
using FCG.Application.Options;
using FCG.Domain.Entities;
using FCG.Domain.Enums;
using FCG.Domain.ValueObjects;
using FCG.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FCG.Tests.Unit.Infrastructure.Services;

public class JwtTokenServiceTests
{
    private const string SigningKey = "chave-de-teste-com-tamanho-minimo-de-32-caracteres-ok";
    private const string Issuer = "FcgApi.Tests";
    private const string Audience = "FcgClients.Tests";

    private readonly JwtSettings _settings = new()
    {
        Issuer = Issuer,
        Audience = Audience,
        SigningKey = SigningKey,
        AccessTokenExpirationMinutes = 60,
        RefreshTokenExpirationDays = 7
    };

    private readonly JwtTokenService _service;
    private readonly Usuario _usuario;

    public JwtTokenServiceTests()
    {
        _service = new JwtTokenService(Options.Create(_settings));
        _usuario = Usuario.Criar(
            "João Silva",
            Email.Criar("joao@email.com"),
            SenhaHash.Reconstituir("$2a$11$hash"));
    }

    [Fact]
    public void DeveGerarTokenContendoClaimSubComIdDoUsuario()
    {
        var resultado = _service.GerarAccessToken(_usuario);
        var token = LerToken(resultado.Token);

        token.Subject.Should().Be(_usuario.Id.ToString());
    }

    [Fact]
    public void DeveGerarTokenContendoClaimEmail()
    {
        var resultado = _service.GerarAccessToken(_usuario);
        var token = LerToken(resultado.Token);

        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == _usuario.Email.Endereco);
    }

    [Fact]
    public void DeveGerarTokenContendoClaimNameComNomeDoUsuario()
    {
        var resultado = _service.GerarAccessToken(_usuario);
        var token = LerToken(resultado.Token);

        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Name && c.Value == _usuario.Nome);
    }

    [Fact]
    public void DeveGerarTokenContendoClaimRoleComoUsuario()
    {
        var resultado = _service.GerarAccessToken(_usuario);
        var token = LerToken(resultado.Token);

        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == TipoUsuario.Usuario.ToString());
    }

    [Fact]
    public void DeveGerarTokenContendoClaimRoleComoAdministrador()
    {
        var admin = Usuario.Criar(
            "Admin",
            Email.Criar("admin@email.com"),
            SenhaHash.Reconstituir("$2a$11$hash"),
            TipoUsuario.Administrador);

        var resultado = _service.GerarAccessToken(admin);
        var token = LerToken(resultado.Token);

        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == TipoUsuario.Administrador.ToString());
    }

    [Fact]
    public void DeveGerarTokenContendoJti()
    {
        var resultado = _service.GerarAccessToken(_usuario);
        var token = LerToken(resultado.Token);

        var jti = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        jti.Should().NotBeNull();
        Guid.TryParse(jti!.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void DeveGerarTokenComExpiracaoConformeConfiguracao()
    {
        var resultado = _service.GerarAccessToken(_usuario);

        resultado.ExpiraEm.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeveGerarTokenComIssuerEAudienceConfigurados()
    {
        var resultado = _service.GerarAccessToken(_usuario);
        var token = LerToken(resultado.Token);

        token.Issuer.Should().Be(Issuer);
        token.Audiences.Should().Contain(Audience);
    }

    [Fact]
    public void DeveCalcularExpiresInEmSegundosCorretamente()
    {
        var resultado = _service.GerarAccessToken(_usuario);

        resultado.ExpiresInSeconds.Should().Be(60 * 60);
    }

    [Fact]
    public void DeveAssinarTokenComHmacSha256()
    {
        var resultado = _service.GerarAccessToken(_usuario);
        var token = LerToken(resultado.Token);

        token.SignatureAlgorithm.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void DeveGerarTokenValidavelComMesmaChave()
    {
        var resultado = _service.GerarAccessToken(_usuario);

        var handler = new JwtSecurityTokenHandler();
        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ClockSkew = TimeSpan.Zero
        };

        var acao = () => handler.ValidateToken(resultado.Token, parametros, out _);
        acao.Should().NotThrow();
    }

    [Fact]
    public void DeveGerarRefreshTokenComPlaintextEHashDistintos()
    {
        RefreshTokenGerado gerado = _service.GerarRefreshToken();

        gerado.Plaintext.Should().NotBeNullOrWhiteSpace();
        gerado.Hash.Should().NotBeNullOrWhiteSpace();
        gerado.Plaintext.Should().NotBe(gerado.Hash);
    }

    [Fact]
    public void DeveGerarRefreshTokensDistintosACadaChamada()
    {
        RefreshTokenGerado a = _service.GerarRefreshToken();
        RefreshTokenGerado b = _service.GerarRefreshToken();

        a.Plaintext.Should().NotBe(b.Plaintext);
        a.Hash.Should().NotBe(b.Hash);
    }

    [Fact]
    public void DeveGerarRefreshTokenComExpiracaoConformeConfiguracao()
    {
        RefreshTokenGerado gerado = _service.GerarRefreshToken();

        gerado.ExpiraEm.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeveCalcularHashDeterministico()
    {
        string a = _service.CalcularHashRefreshToken("token-de-teste");
        string b = _service.CalcularHashRefreshToken("token-de-teste");

        a.Should().Be(b);
    }

    [Fact]
    public void DeveCalcularHashDiferenteParaPlaintextsDiferentes()
    {
        string a = _service.CalcularHashRefreshToken("token-A");
        string b = _service.CalcularHashRefreshToken("token-B");

        a.Should().NotBe(b);
    }

    [Fact]
    public void DeveCalcularHashCompativelComOPlaintextDoRefreshTokenGerado()
    {
        RefreshTokenGerado gerado = _service.GerarRefreshToken();

        string hash = _service.CalcularHashRefreshToken(gerado.Plaintext);

        hash.Should().Be(gerado.Hash);
    }

    private static JwtSecurityToken LerToken(string tokenString)
    {
        return new JwtSecurityTokenHandler().ReadJwtToken(tokenString);
    }
}
