using FCG.Domain.Entities;
using FCG.Domain.Exceptions;
using FCG.Domain.Interfaces;
using FCG.Domain.Services;
using FCG.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace FCG.Tests.Unit.Domain.Services;

public class UsuarioDomainServiceTests
{
    private const string NomeValido = "João Silva";
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly UsuarioDomainService _domainService;

    private readonly Email _emailValido = Email.Criar("joao@email.com");
    private readonly SenhaHash _senhaHashValida = SenhaHash.Reconstituir(
        "$2a$11$hashFalsoParaTestes"
    );

    public UsuarioDomainServiceTests()
    {
        _repositorioMock
            .Setup(r => r.ExisteComEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _domainService = new UsuarioDomainService(_repositorioMock.Object);
    }

    [Fact]
    public async Task DeveRegistrarUsuarioComDadosValidos()
    {
        Usuario usuario = await _domainService.RegistrarAsync(
            NomeValido,
            _emailValido,
            _senhaHashValida
        );

        usuario.Nome.Should().Be(NomeValido);
        usuario.Email.Should().Be(_emailValido);
        usuario.SenhaHash.Should().Be(_senhaHashValida);
        usuario.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeveLancarDomainConflictExceptionParaEmailDuplicado()
    {
        _repositorioMock
            .Setup(r => r.ExisteComEmailAsync(_emailValido, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Func<Task<Usuario>> acao = () =>
            _domainService.RegistrarAsync(NomeValido, _emailValido, _senhaHashValida);

        await acao.Should().ThrowAsync<DomainConflictException>().WithMessage("*e-mail*");
    }

    [Fact]
    public async Task DeveConsultarRepositorioParaVerificarUnicidadeDeEmail()
    {
        await _domainService.RegistrarAsync(NomeValido, _emailValido, _senhaHashValida);

        _repositorioMock.Verify(
            r => r.ExisteComEmailAsync(_emailValido, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task DeveAtualizarDadosQuandoEmailNaoMudou()
    {
        var usuario = FCG.Domain.Entities.Usuario.Criar(NomeValido, _emailValido, _senhaHashValida);

        await _domainService.AtualizarDadosAsync(usuario, "Novo Nome", _emailValido);

        usuario.Nome.Should().Be("Novo Nome");
        usuario.Email.Should().Be(_emailValido);
    }

    [Fact]
    public async Task NaoDeveConsultarUnicidadeQuandoEmailNaoMudou()
    {
        var usuario = FCG.Domain.Entities.Usuario.Criar(NomeValido, _emailValido, _senhaHashValida);

        await _domainService.AtualizarDadosAsync(usuario, "Novo Nome", _emailValido);

        _repositorioMock.Verify(
            r => r.ExisteComEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeveAtualizarDadosQuandoEmailNovoNaoEstaEmUso()
    {
        var usuario = FCG.Domain.Entities.Usuario.Criar(NomeValido, _emailValido, _senhaHashValida);
        var emailNovo = Email.Criar("novo@email.com");

        await _domainService.AtualizarDadosAsync(usuario, "Novo Nome", emailNovo);

        usuario.Nome.Should().Be("Novo Nome");
        usuario.Email.Should().Be(emailNovo);
    }

    [Fact]
    public async Task DeveLancarDomainConflictExceptionQuandoEmailNovoJaEstaEmUso()
    {
        var emailNovo = Email.Criar("novo@email.com");
        _repositorioMock
            .Setup(r => r.ExisteComEmailAsync(emailNovo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var usuario = FCG.Domain.Entities.Usuario.Criar(NomeValido, _emailValido, _senhaHashValida);

        Func<Task> acao = () => _domainService.AtualizarDadosAsync(usuario, "Novo Nome", emailNovo);

        await acao.Should().ThrowAsync<DomainConflictException>().WithMessage("*e-mail*");
    }

    [Fact]
    public async Task DeveConsultarUnicidadeApenasQuandoEmailMudou()
    {
        var emailNovo = Email.Criar("novo@email.com");
        var usuario = FCG.Domain.Entities.Usuario.Criar(NomeValido, _emailValido, _senhaHashValida);

        await _domainService.AtualizarDadosAsync(usuario, "Novo Nome", emailNovo);

        _repositorioMock.Verify(
            r => r.ExisteComEmailAsync(emailNovo, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
