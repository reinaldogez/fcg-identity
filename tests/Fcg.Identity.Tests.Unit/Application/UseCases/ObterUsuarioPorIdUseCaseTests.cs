using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.UseCases;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Enums;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Fcg.Identity.Tests.Unit.Application.UseCases;

public class ObterUsuarioPorIdUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly ObterUsuarioPorIdUseCase _useCase;

    public ObterUsuarioPorIdUseCaseTests() =>
        _useCase = new ObterUsuarioPorIdUseCase(_repositorioMock.Object);

    [Fact]
    public async Task DeveRetornarUsuarioQuandoEncontrado()
    {
        var id = Guid.NewGuid();
        var email = Email.Reconstituir("joao@email.com");
        var senhaHash = SenhaHash.Reconstituir("hash");
        var usuario = Usuario.Criar("João Silva", email, senhaHash);

        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        UsuarioResponse? resultado = await _useCase.ExecutarAsync(id);

        resultado.Should().NotBeNull();
        resultado!.Nome.Should().Be("João Silva");
        resultado.Email.Should().Be("joao@email.com");
        resultado.Tipo.Should().Be(TipoUsuario.Usuario.ToString());
        resultado.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task DeveRetornarNullQuandoNaoEncontrado()
    {
        var id = Guid.NewGuid();

        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        UsuarioResponse? resultado = await _useCase.ExecutarAsync(id);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task DeveChamarRepositorioComIdCorreto()
    {
        var id = Guid.NewGuid();

        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        await _useCase.ExecutarAsync(id);

        _repositorioMock.Verify(
            r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
