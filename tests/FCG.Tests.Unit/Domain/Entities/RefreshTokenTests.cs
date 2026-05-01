using FCG.Domain.Entities;
using FCG.Domain.Exceptions;
using FluentAssertions;

namespace FCG.Tests.Unit.Domain.Entities;

public class RefreshTokenTests
{
    private const string Hash = "hash-valido";
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly DateTime ExpiraEm = DateTime.UtcNow.AddDays(7);

    [Fact]
    public void DeveCriarRefreshTokenComDadosValidos()
    {
        RefreshToken token = RefreshToken.Criar(UsuarioId, Hash, ExpiraEm);

        token.Id.Should().NotBe(Guid.Empty);
        token.UsuarioId.Should().Be(UsuarioId);
        token.TokenHash.Should().Be(Hash);
        token.ExpiraEm.Should().Be(ExpiraEm);
        token.RevogadoEm.Should().BeNull();
        token.SubstituidoPor.Should().BeNull();
        token.EstaAtivo.Should().BeTrue();
    }

    [Fact]
    public void DeveRejeitarUsuarioIdVazio()
    {
        Action acao = () => RefreshToken.Criar(Guid.Empty, Hash, ExpiraEm);

        acao.Should().Throw<DomainException>().WithMessage("*usuário*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void DeveRejeitarHashVazio(string? hash)
    {
        Action acao = () => RefreshToken.Criar(UsuarioId, hash!, ExpiraEm);

        acao.Should().Throw<DomainException>().WithMessage("*hash*");
    }

    [Fact]
    public void DeveRejeitarExpiracaoNoPassado()
    {
        Action acao = () => RefreshToken.Criar(UsuarioId, Hash, DateTime.UtcNow.AddSeconds(-1));

        acao.Should().Throw<DomainException>().WithMessage("*futura*");
    }

    [Fact]
    public async Task EstaAtivoDeveSerFalseQuandoExpirado()
    {
        RefreshToken token = RefreshToken.Criar(
            UsuarioId,
            Hash,
            DateTime.UtcNow.AddMilliseconds(50)
        );

        await Task.Delay(100);

        token.EstaAtivo.Should().BeFalse();
    }

    [Fact]
    public void EstaAtivoDeveSerFalseQuandoRevogado()
    {
        RefreshToken token = RefreshToken.Criar(UsuarioId, Hash, ExpiraEm);

        token.Revogar();

        token.EstaAtivo.Should().BeFalse();
        token.RevogadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task RevogarDeveSerIdempotente()
    {
        RefreshToken token = RefreshToken.Criar(UsuarioId, Hash, ExpiraEm);
        token.Revogar();
        DateTime primeiro = token.RevogadoEm!.Value;

        await Task.Delay(20);
        token.Revogar();

        token.RevogadoEm.Should().Be(primeiro);
    }

    [Fact]
    public void RevogarESubstituirPorDeveMarcarSubstituicao()
    {
        RefreshToken token = RefreshToken.Criar(UsuarioId, Hash, ExpiraEm);
        Guid substitutoId = Guid.NewGuid();

        token.RevogarESubstituirPor(substitutoId);

        token.RevogadoEm.Should().NotBeNull();
        token.SubstituidoPor.Should().Be(substitutoId);
        token.EstaAtivo.Should().BeFalse();
    }

    [Fact]
    public void RevogarESubstituirPorDeveLancarSeJaRevogado()
    {
        RefreshToken token = RefreshToken.Criar(UsuarioId, Hash, ExpiraEm);
        token.Revogar();

        Action acao = () => token.RevogarESubstituirPor(Guid.NewGuid());

        acao.Should().Throw<DomainException>().WithMessage("*já*revogado*");
    }

    [Fact]
    public void RevogarESubstituirPorDeveRejeitarSubstitutoVazio()
    {
        RefreshToken token = RefreshToken.Criar(UsuarioId, Hash, ExpiraEm);

        Action acao = () => token.RevogarESubstituirPor(Guid.Empty);

        acao.Should().Throw<DomainException>().WithMessage("*substituto*");
    }
}
