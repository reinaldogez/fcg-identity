using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Exceptions;
using FluentAssertions;

namespace Fcg.Identity.Tests.Unit.Domain.Entities;

public class RefreshTokenTests
{
    private const string Hash = "hash-valido";
    private static readonly Guid _usuarioId = Guid.NewGuid();
    private static readonly DateTime _expiraEm = DateTime.UtcNow.AddDays(7);

    [Fact]
    public void DeveCriarRefreshTokenComDadosValidos()
    {
        var token = RefreshToken.Criar(_usuarioId, Hash, _expiraEm);

        token.Id.Should().NotBe(Guid.Empty);
        token.UsuarioId.Should().Be(_usuarioId);
        token.TokenHash.Should().Be(Hash);
        token.ExpiraEm.Should().Be(_expiraEm);
        token.RevogadoEm.Should().BeNull();
        token.SubstituidoPor.Should().BeNull();
        token.EstaAtivo.Should().BeTrue();
    }

    [Fact]
    public void DeveRejeitarUsuarioIdVazio()
    {
        Action acao = () => RefreshToken.Criar(Guid.Empty, Hash, _expiraEm);

        acao.Should().Throw<DomainException>().WithMessage("*usuário*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void DeveRejeitarHashVazio(string? hash)
    {
        Action acao = () => RefreshToken.Criar(_usuarioId, hash!, _expiraEm);

        acao.Should().Throw<DomainException>().WithMessage("*hash*");
    }

    [Fact]
    public void DeveRejeitarExpiracaoNoPassado()
    {
        Action acao = () => RefreshToken.Criar(_usuarioId, Hash, DateTime.UtcNow.AddSeconds(-1));

        acao.Should().Throw<DomainException>().WithMessage("*futura*");
    }

    [Fact]
    public async Task EstaAtivoDeveSerFalseQuandoExpirado()
    {
        var token = RefreshToken.Criar(_usuarioId, Hash, DateTime.UtcNow.AddMilliseconds(50));

        await Task.Delay(100);

        token.EstaAtivo.Should().BeFalse();
    }

    [Fact]
    public void EstaAtivoDeveSerFalseQuandoRevogado()
    {
        var token = RefreshToken.Criar(_usuarioId, Hash, _expiraEm);

        token.Revogar();

        token.EstaAtivo.Should().BeFalse();
        token.RevogadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task RevogarDeveSerIdempotente()
    {
        var token = RefreshToken.Criar(_usuarioId, Hash, _expiraEm);
        token.Revogar();
        DateTime primeiro = token.RevogadoEm!.Value;

        await Task.Delay(20);
        token.Revogar();

        token.RevogadoEm.Should().Be(primeiro);
    }

    [Fact]
    public void RevogarESubstituirPorDeveMarcarSubstituicao()
    {
        var token = RefreshToken.Criar(_usuarioId, Hash, _expiraEm);
        var substitutoId = Guid.NewGuid();

        token.RevogarESubstituirPor(substitutoId);

        token.RevogadoEm.Should().NotBeNull();
        token.SubstituidoPor.Should().Be(substitutoId);
        token.EstaAtivo.Should().BeFalse();
    }

    [Fact]
    public void RevogarESubstituirPorDeveLancarSeJaRevogado()
    {
        var token = RefreshToken.Criar(_usuarioId, Hash, _expiraEm);
        token.Revogar();

        Action acao = () => token.RevogarESubstituirPor(Guid.NewGuid());

        acao.Should().Throw<DomainException>().WithMessage("*já*revogado*");
    }

    [Fact]
    public void RevogarESubstituirPorDeveRejeitarSubstitutoVazio()
    {
        var token = RefreshToken.Criar(_usuarioId, Hash, _expiraEm);

        Action acao = () => token.RevogarESubstituirPor(Guid.Empty);

        acao.Should().Throw<DomainException>().WithMessage("*substituto*");
    }
}
