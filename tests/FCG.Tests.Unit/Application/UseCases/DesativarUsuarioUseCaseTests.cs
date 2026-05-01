using FCG.Application.UseCases;
using FCG.Domain.Entities;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace FCG.Tests.Unit.Application.UseCases;

public class DesativarUsuarioUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DesativarUsuarioUseCase _useCase;

    public DesativarUsuarioUseCaseTests()
    {
        _useCase = new DesativarUsuarioUseCase(_repositorioMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task DeveDesativarUsuarioAtivo()
    {
        var usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        var resultado = await _useCase.ExecutarAsync(usuario.Id);

        resultado.Should().BeTrue();
        usuario.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task DeveRetornarFalseQuandoUsuarioNaoEncontrado()
    {
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var resultado = await _useCase.ExecutarAsync(Guid.NewGuid());

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task DeveSerIdempotenteParaUsuarioJaDesativado()
    {
        var usuario = CriarUsuario();
        usuario.Desativar();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        var acao = () => _useCase.ExecutarAsync(usuario.Id);

        await acao.Should().NotThrowAsync();
        var resultado = await _useCase.ExecutarAsync(usuario.Id);
        resultado.Should().BeTrue();
        usuario.Ativo.Should().BeFalse();
        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeveChamarSalvarAlteracoes()
    {
        var usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        await _useCase.ExecutarAsync(usuario.Id);

        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private static Usuario CriarUsuario() =>
        Usuario.Criar(
            "Nome",
            Email.Criar("teste@email.com"),
            SenhaHash.Reconstituir("$2a$11$hashFalso")
        );
}
