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

    [Fact]
    public void DeveAlterarNomeEEmailQuandoDadosValidos()
    {
        var usuario = Usuario.Criar("Nome Original", _emailValido, _senhaHashValida);
        var novoEmail = Email.Criar("novo@email.com");

        usuario.AlterarDados("Nome Novo", novoEmail);

        usuario.Nome.Should().Be("Nome Novo");
        usuario.Email.Should().Be(novoEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeveRejeitarNomeVazioAoAlterarDados(string? nome)
    {
        var usuario = Usuario.Criar("Nome Original", _emailValido, _senhaHashValida);
        var novoEmail = Email.Criar("novo@email.com");

        var acao = () => usuario.AlterarDados(nome!, novoEmail);

        acao.Should().Throw<DomainException>().WithMessage("*nome*");
    }

    [Fact]
    public void DeveRejeitarNomeAcimaDoTamanhoMaximoAoAlterarDados()
    {
        var usuario = Usuario.Criar("Nome Original", _emailValido, _senhaHashValida);
        var nome = new string('A', Usuario.NomeTamanhoMaximo + 1);

        var acao = () => usuario.AlterarDados(nome, _emailValido);

        acao.Should().Throw<DomainException>().WithMessage("*máximo*");
    }

    [Fact]
    public void DeveRejeitarEmailNuloAoAlterarDados()
    {
        var usuario = Usuario.Criar("Nome Original", _emailValido, _senhaHashValida);

        var acao = () => usuario.AlterarDados("Nome Novo", null!);

        acao.Should().Throw<DomainException>().WithMessage("*e-mail*");
    }

    [Fact]
    public void DeveTrimarNomeAoAlterarDados()
    {
        var usuario = Usuario.Criar("Nome Original", _emailValido, _senhaHashValida);

        usuario.AlterarDados("  Nome Com Espaços  ", _emailValido);

        usuario.Nome.Should().Be("Nome Com Espaços");
    }

    [Fact]
    public void DeveAlterarSenhaHashQuandoNovoHashValido()
    {
        var usuario = Usuario.Criar("Nome", _emailValido, _senhaHashValida);
        var novoHash = SenhaHash.Reconstituir("$2a$11$novoHashParaTestes");

        usuario.AlterarSenha(novoHash);

        usuario.SenhaHash.Should().Be(novoHash);
    }

    [Fact]
    public void DeveMarcarUsuarioComoInativoAoDesativar()
    {
        var usuario = Usuario.Criar("Nome", _emailValido, _senhaHashValida);

        usuario.Desativar();

        usuario.Ativo.Should().BeFalse();
    }

    [Fact]
    public void DeveSerIdempotenteAoDesativarUsuarioJaInativo()
    {
        var usuario = Usuario.Criar("Nome", _emailValido, _senhaHashValida);
        usuario.Desativar();

        var acao = () => usuario.Desativar();

        acao.Should().NotThrow();
        usuario.Ativo.Should().BeFalse();
    }

    [Fact]
    public void DeveAlterarTipoParaAdministrador()
    {
        var usuario = Usuario.Criar("Nome", _emailValido, _senhaHashValida);

        usuario.AlterarTipo(TipoUsuario.Administrador);

        usuario.Tipo.Should().Be(TipoUsuario.Administrador);
    }

    [Fact]
    public void DeveAlterarTipoParaUsuarioComum()
    {
        var usuario = Usuario.Criar("Nome", _emailValido, _senhaHashValida, TipoUsuario.Administrador);

        usuario.AlterarTipo(TipoUsuario.Usuario);

        usuario.Tipo.Should().Be(TipoUsuario.Usuario);
    }

    [Fact]
    public void DeveRejeitarTipoInvalido()
    {
        var usuario = Usuario.Criar("Nome", _emailValido, _senhaHashValida);

        var acao = () => usuario.AlterarTipo((TipoUsuario)99);

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void NaoDevePermitirAdministradorRebaixarASiMesmo()
    {
        var admin = Usuario.Criar("Admin", _emailValido, _senhaHashValida, TipoUsuario.Administrador);

        var acao = () => admin.AlterarTipoSolicitadoPor(TipoUsuario.Usuario, admin.Id);

        acao.Should().Throw<DomainException>().WithMessage("*rebaixar a si mesmo*");
    }

    [Fact]
    public void DevePermitirAdministradorRebaixarOutroAdministrador()
    {
        var alvo = Usuario.Criar("Outro", _emailValido, _senhaHashValida, TipoUsuario.Administrador);
        var solicitanteId = Guid.NewGuid();

        alvo.AlterarTipoSolicitadoPor(TipoUsuario.Usuario, solicitanteId);

        alvo.Tipo.Should().Be(TipoUsuario.Usuario);
    }

    [Fact]
    public void DevePermitirAdministradorPromoverASiMesmoQuandoTipoIgual()
    {
        var admin = Usuario.Criar("Admin", _emailValido, _senhaHashValida, TipoUsuario.Administrador);

        var acao = () => admin.AlterarTipoSolicitadoPor(TipoUsuario.Administrador, admin.Id);

        acao.Should().NotThrow();
        admin.Tipo.Should().Be(TipoUsuario.Administrador);
    }

    [Fact]
    public void DevePermitirUsuarioComumPromoverASiMesmoSemRestricao()
    {
        var usuario = Usuario.Criar("Comum", _emailValido, _senhaHashValida);

        usuario.AlterarTipoSolicitadoPor(TipoUsuario.Administrador, usuario.Id);

        usuario.Tipo.Should().Be(TipoUsuario.Administrador);
    }

    [Fact]
    public void DeveRejeitarAlterarTipoSolicitadoPorComTipoInvalido()
    {
        var admin = Usuario.Criar("Admin", _emailValido, _senhaHashValida, TipoUsuario.Administrador);

        var acao = () => admin.AlterarTipoSolicitadoPor((TipoUsuario)99, Guid.NewGuid());

        acao.Should().Throw<DomainException>().WithMessage("*Tipo*");
    }
}
