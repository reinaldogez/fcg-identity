using FCG.Application.DTOs;
using FCG.Application.Interfaces;
using FCG.Application.UseCases;
using FCG.Domain.Entities;
using FCG.Domain.Exceptions;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace FCG.Tests.Unit.Application.UseCases;

public class AlterarSenhaUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly Mock<ISenhaService> _senhaServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AlterarSenhaUseCase _useCase;

    private readonly SenhaHash _senhaHashOriginal = SenhaHash.Reconstituir("$2a$11$hashOriginal");

    public AlterarSenhaUseCaseTests()
    {
        _senhaServiceMock
            .Setup(s => s.GerarHash(It.IsAny<string>()))
            .Returns(SenhaHash.Reconstituir("$2a$11$novoHash"));

        _useCase = new AlterarSenhaUseCase(
            _repositorioMock.Object,
            _senhaServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    private Usuario CriarUsuario() =>
        Usuario.Criar("Nome", Email.Criar("teste@email.com"), _senhaHashOriginal);

    [Fact]
    public async Task DeveAlterarSenhaQuandoSenhaAtualCorretaENovaValida()
    {
        var usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha("SenhaAtual@1", _senhaHashOriginal))
            .Returns(true);

        var resultado = await _useCase.ExecutarAsync(
            usuario.Id, new AlterarSenhaRequest("SenhaAtual@1", "NovaSenha@2"));

        resultado.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeveRetornarFalseQuandoUsuarioNaoEncontrado()
    {
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var resultado = await _useCase.ExecutarAsync(
            Guid.NewGuid(), new AlterarSenhaRequest("Senha@1", "NovaSenha@2"));

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task DeveLancarDomainExceptionQuandoSenhaAtualIncorreta()
    {
        var usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(false);

        var acao = () => _useCase.ExecutarAsync(
            usuario.Id, new AlterarSenhaRequest("SenhaErrada@1", "NovaSenha@2"));

        await acao.Should().ThrowAsync<DomainException>()
            .WithMessage("*senha atual*");
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("semNumero@")]
    [InlineData("semEspecial1")]
    public async Task DeveRejeitarNovaSenhaFraca(string novaSenha)
    {
        var usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(true);

        var acao = () => _useCase.ExecutarAsync(
            usuario.Id, new AlterarSenhaRequest("SenhaAtual@1", novaSenha));

        await acao.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DeveChamarVerificarSenhaComSenhaAtual()
    {
        var usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha("SenhaAtual@1", _senhaHashOriginal))
            .Returns(true);

        await _useCase.ExecutarAsync(
            usuario.Id, new AlterarSenhaRequest("SenhaAtual@1", "NovaSenha@2"));

        _senhaServiceMock.Verify(s => s.VerificarSenha("SenhaAtual@1", _senhaHashOriginal), Times.Once);
    }

    [Fact]
    public async Task DeveChamarGerarHashComNovaSenha()
    {
        var usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(true);

        await _useCase.ExecutarAsync(
            usuario.Id, new AlterarSenhaRequest("SenhaAtual@1", "NovaSenha@2"));

        _senhaServiceMock.Verify(s => s.GerarHash("NovaSenha@2"), Times.Once);
    }

    [Fact]
    public async Task NaoDeveSalvarQuandoSenhaAtualIncorreta()
    {
        var usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(false);

        try
        {
            await _useCase.ExecutarAsync(
                usuario.Id, new AlterarSenhaRequest("SenhaErrada@1", "NovaSenha@2"));
        }
        catch (DomainException) { }

        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
