using FCG.Application.UseCases;
using FCG.Domain.Entities;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace FCG.Tests.Unit.Application.UseCases;

public class ListarUsuariosUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly ListarUsuariosUseCase _useCase;

    private static Usuario CriarUsuario(string email = "teste@email.com")
    {
        return Usuario.Criar(
            "Nome Teste",
            Email.Criar(email),
            SenhaHash.Reconstituir("$2a$11$hashFalso"));
    }

    public ListarUsuariosUseCaseTests()
    {
        _useCase = new ListarUsuariosUseCase(_repositorioMock.Object);
    }

    [Fact]
    public async Task DeveRetornarListaPaginadaComTotal()
    {
        var usuarios = new List<Usuario>
        {
            CriarUsuario("a@email.com"),
            CriarUsuario("b@email.com"),
        };
        _repositorioMock
            .Setup(r => r.ListarPaginadoAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((usuarios, 5));

        var resultado = await _useCase.ExecutarAsync(1, 10);

        resultado.Items.Should().HaveCount(2);
        resultado.Total.Should().Be(5);
        resultado.Pagina.Should().Be(1);
        resultado.TamanhoPagina.Should().Be(10);
    }

    [Fact]
    public async Task DeveRetornarListaVaziaQuandoNaoHaUsuarios()
    {
        _repositorioMock
            .Setup(r => r.ListarPaginadoAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Usuario>(), 0));

        var resultado = await _useCase.ExecutarAsync(1, 10);

        resultado.Items.Should().BeEmpty();
        resultado.Total.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task DeveAceitarTamanhoPaginaNoLimite(int tamanhoPagina)
    {
        _repositorioMock
            .Setup(r => r.ListarPaginadoAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Usuario>(), 0));

        var acao = () => _useCase.ExecutarAsync(1, tamanhoPagina);

        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeveChamarRepositorioComParametrosCorretos()
    {
        _repositorioMock
            .Setup(r => r.ListarPaginadoAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Usuario>(), 0));

        await _useCase.ExecutarAsync(2, 15);

        _repositorioMock.Verify(
            r => r.ListarPaginadoAsync(2, 15, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
