using FCG.Domain.Exceptions;
using FCG.Domain.Interfaces;
using FCG.Domain.Services;
using FCG.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace FCG.Tests.Unit.Domain.Services;

public class UsuarioDomainServiceTests
{
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly UsuarioDomainService _domainService;

    private readonly Email _emailValido = Email.Criar("joao@email.com");
    private readonly SenhaHash _senhaHashValida = SenhaHash.Reconstituir("$2a$11$hashFalsoParaTestes");
    private const string NomeValido = "João Silva";

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
        var usuario = await _domainService.RegistrarAsync(NomeValido, _emailValido, _senhaHashValida);

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

        var acao = () => _domainService.RegistrarAsync(NomeValido, _emailValido, _senhaHashValida);

        await acao.Should().ThrowAsync<DomainConflictException>()
            .WithMessage("*e-mail*");
    }

    [Fact]
    public async Task DeveConsultarRepositorioParaVerificarUnicidadeDeEmail()
    {
        await _domainService.RegistrarAsync(NomeValido, _emailValido, _senhaHashValida);

        _repositorioMock.Verify(
            r => r.ExisteComEmailAsync(_emailValido, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
