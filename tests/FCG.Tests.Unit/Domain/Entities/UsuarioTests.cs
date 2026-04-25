using FCG.Domain.Entities;
using FCG.Domain.Enums;
using FCG.Domain.Exceptions;
using FCG.Domain.ValueObjects;
using FluentAssertions;

namespace FCG.Tests.Unit.Domain.Entities;

public class UsuarioTests
{
    private readonly Email _emailValido = Email.Criar("teste@email.com");
    private readonly SenhaHash _senhaHashValida = SenhaHash.Reconstituir("$2a$11$hashFalsoParaTestes");

    [Fact]
    public void DeveCriarUsuarioComDadosValidos()
    {
        var usuario = Usuario.Criar("João Silva", _emailValido, _senhaHashValida);

        usuario.Nome.Should().Be("João Silva");
        usuario.Email.Should().Be(_emailValido);
        usuario.SenhaHash.Should().Be(_senhaHashValida);
    }

    [Fact]
    public void DeveAtribuirTipoUsuarioPadrao()
    {
        var usuario = Usuario.Criar("João Silva", _emailValido, _senhaHashValida);

        usuario.Tipo.Should().Be(TipoUsuario.Usuario);
    }

    [Fact]
    public void DeveDefinirDataCriacaoAutomaticamente()
    {
        var antes = DateTime.UtcNow;

        var usuario = Usuario.Criar("João Silva", _emailValido, _senhaHashValida);

        var depois = DateTime.UtcNow;
        usuario.DataCriacao.Should().BeOnOrAfter(antes).And.BeOnOrBefore(depois);
    }

    [Fact]
    public void DeveSerAtivoPorPadrao()
    {
        var usuario = Usuario.Criar("João Silva", _emailValido, _senhaHashValida);

        usuario.Ativo.Should().BeTrue();
    }

    [Fact]
    public void DeveGerarIdAutomaticamente()
    {
        var usuario = Usuario.Criar("João Silva", _emailValido, _senhaHashValida);

        usuario.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeveRejeitarNomeVazio(string? nome)
    {
        var acao = () => Usuario.Criar(nome!, _emailValido, _senhaHashValida);

        acao.Should().Throw<DomainException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void DeveAceitarNomeComTamanhoMaximo()
    {
        var nome = new string('A', Usuario.NomeTamanhoMaximo);

        var acao = () => Usuario.Criar(nome, _emailValido, _senhaHashValida);

        acao.Should().NotThrow();
    }

    [Fact]
    public void DeveRejeitarNomeAcimaDoTamanhoMaximo()
    {
        var nome = new string('A', Usuario.NomeTamanhoMaximo + 1);

        var acao = () => Usuario.Criar(nome, _emailValido, _senhaHashValida);

        acao.Should().Throw<DomainException>()
            .WithMessage("*máximo*");
    }

    [Fact]
    public void DeveCriarAdministradorComTipoCorreto()
    {
        var admin = Usuario.Criar("Admin", _emailValido, _senhaHashValida, TipoUsuario.Administrador);

        admin.Tipo.Should().Be(TipoUsuario.Administrador);
        admin.Nome.Should().Be("Admin");
        admin.Email.Should().Be(_emailValido);
        admin.Ativo.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeveRejeitarNomeVazioParaAdministrador(string? nome)
    {
        var acao = () => Usuario.Criar(nome!, _emailValido, _senhaHashValida, TipoUsuario.Administrador);

        acao.Should().Throw<DomainException>()
            .WithMessage("*nome*");
    }
}
