using Fcg.Identity.Application.UseCases;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Fcg.Identity.Tests.Unit.Application.UseCases;

public class DesativarUsuarioUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DesativarUsuarioUseCase _useCase;

    public DesativarUsuarioUseCaseTests() =>
        _useCase = new DesativarUsuarioUseCase(_repositorioMock.Object, _unitOfWorkMock.Object);

    [Fact]
    public async Task DeveDesativarUsuarioAtivo()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        bool resultado = await _useCase.ExecutarAsync(usuario.Id);

        resultado.Should().BeTrue();
        usuario.Ativo.Should().BeFalse();
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
    public async Task DeveSerIdempotenteParaUsuarioJaDesativado()
    {
        Usuario usuario = CriarUsuario();
        usuario.Desativar();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        Func<Task<bool>> acao = () => _useCase.ExecutarAsync(usuario.Id);

        await acao.Should().NotThrowAsync();
        bool resultado = await _useCase.ExecutarAsync(usuario.Id);
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
        Usuario usuario = CriarUsuario();
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
