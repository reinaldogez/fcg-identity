using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Application.UseCases;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Exceptions;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Fcg.Identity.Tests.Unit.Application.UseCases;

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
            _unitOfWorkMock.Object
        );
    }

    [Fact]
    public async Task DeveAlterarSenhaQuandoSenhaAtualCorretaENovaValida()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha("SenhaAtual@1", _senhaHashOriginal))
            .Returns(true);

        bool resultado = await _useCase.ExecutarAsync(
            usuario.Id,
            new AlterarSenhaRequest("SenhaAtual@1", "NovaSenha@2")
        );

        resultado.Should().BeTrue();
        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task DeveRetornarFalseQuandoUsuarioNaoEncontrado()
    {
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        bool resultado = await _useCase.ExecutarAsync(
            Guid.NewGuid(),
            new AlterarSenhaRequest("Senha@1", "NovaSenha@2")
        );

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task DeveLancarDomainExceptionQuandoSenhaAtualIncorreta()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(false);

        Func<Task<bool>> acao = () =>
            _useCase.ExecutarAsync(
                usuario.Id,
                new AlterarSenhaRequest("SenhaErrada@1", "NovaSenha@2")
            );

        await acao.Should().ThrowAsync<DomainException>().WithMessage("*senha atual*");
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("semNumero@")]
    [InlineData("semEspecial1")]
    public async Task DeveRejeitarNovaSenhaFraca(string novaSenha)
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(true);

        Func<Task<bool>> acao = () =>
            _useCase.ExecutarAsync(usuario.Id, new AlterarSenhaRequest("SenhaAtual@1", novaSenha));

        await acao.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DeveChamarVerificarSenhaComSenhaAtual()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha("SenhaAtual@1", _senhaHashOriginal))
            .Returns(true);

        await _useCase.ExecutarAsync(
            usuario.Id,
            new AlterarSenhaRequest("SenhaAtual@1", "NovaSenha@2")
        );

        _senhaServiceMock.Verify(
            s => s.VerificarSenha("SenhaAtual@1", _senhaHashOriginal),
            Times.Once
        );
    }

    [Fact]
    public async Task DeveChamarGerarHashComNovaSenha()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(true);

        await _useCase.ExecutarAsync(
            usuario.Id,
            new AlterarSenhaRequest("SenhaAtual@1", "NovaSenha@2")
        );

        _senhaServiceMock.Verify(s => s.GerarHash("NovaSenha@2"), Times.Once);
    }

    [Fact]
    public async Task NaoDeveSalvarQuandoSenhaAtualIncorreta()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(false);

        try
        {
            await _useCase.ExecutarAsync(
                usuario.Id,
                new AlterarSenhaRequest("SenhaErrada@1", "NovaSenha@2")
            );
        }
        catch (DomainException)
        {
            // Esperado: senha incorreta lança DomainException
        }

        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private Usuario CriarUsuario() =>
        Usuario.Criar("Nome", Email.Criar("teste@email.com"), _senhaHashOriginal);
}
