using FCG.Application.UseCases;
using FCG.Domain.Entities;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace FCG.Tests.Unit.Application.UseCases;

public class AtivarUsuarioUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AtivarUsuarioUseCase _useCase;

    public AtivarUsuarioUseCaseTests() =>
        _useCase = new AtivarUsuarioUseCase(_repositorioMock.Object, _unitOfWorkMock.Object);

    [Fact]
    public async Task DeveAtivarUsuarioInativo()
    {
        Usuario usuario = CriarUsuario();
        usuario.Desativar();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        bool resultado = await _useCase.ExecutarAsync(usuario.Id);

        resultado.Should().BeTrue();
        usuario.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task DeveRetornarFalseQuandoUsuarioNaoEncontrado()
    {
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        bool resultado = await _useCase.ExecutarAsync(Guid.NewGuid());

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task DeveSerIdempotenteParaUsuarioJaAtivo()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        Func<Task<bool>> acao = () => _useCase.ExecutarAsync(usuario.Id);

        await acao.Should().NotThrowAsync();
        bool resultado = await _useCase.ExecutarAsync(usuario.Id);
        resultado.Should().BeTrue();
        usuario.Ativo.Should().BeTrue();
        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeveChamarSalvarAlteracoes()
    {
        Usuario usuario = CriarUsuario();
        usuario.Desativar();
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
