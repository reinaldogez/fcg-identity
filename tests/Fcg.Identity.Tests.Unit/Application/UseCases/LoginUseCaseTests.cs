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

public class LoginUseCaseTests
{
    private const string MensagemEsperada = "Credenciais inválidas.";

    private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshRepositoryMock = new();
    private readonly Mock<ISenhaService> _senhaServiceMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly LoginUseCase _useCase;
    private readonly Usuario _usuario;

    public LoginUseCaseTests()
    {
        _usuario = Usuario.Criar(
            "João Silva",
            Email.Criar("joao@email.com"),
            SenhaHash.Reconstituir("$2a$11$hash")
        );

        _jwtTokenServiceMock
            .Setup(s => s.GerarAccessToken(It.IsAny<Usuario>()))
            .Returns(new AccessToken("token-jwt", DateTime.UtcNow.AddHours(1), 3600));

        _jwtTokenServiceMock
            .Setup(s => s.GerarRefreshToken())
            .Returns(
                new RefreshTokenGerado("refresh-plain", "refresh-hash", DateTime.UtcNow.AddDays(7))
            );

        _useCase = new LoginUseCase(
            _usuarioRepositoryMock.Object,
            _refreshRepositoryMock.Object,
            _senhaServiceMock.Object,
            _jwtTokenServiceMock.Object,
            _unitOfWorkMock.Object
        );
    }

    [Fact]
    public async Task DeveRetornarAccessTokenQuandoCredenciaisValidas()
    {
        ConfigurarLoginValido();

        LoginResponse resposta = await _useCase.ExecutarAsync(
            new LoginRequest("joao@email.com", "Senh@123")
        );

        resposta.AccessToken.Should().Be("token-jwt");
        resposta.TokenType.Should().Be("Bearer");
        resposta.ExpiresIn.Should().Be(3600);
        resposta.RefreshToken.Should().Be("refresh-plain");
    }

    [Fact]
    public async Task DevePersistirRefreshTokenComHashEUsuarioCorretos()
    {
        ConfigurarLoginValido();
        RefreshToken? capturado = null;
        _refreshRepositoryMock
            .Setup(r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((rt, _) => capturado = rt)
            .Returns(Task.CompletedTask);

        await _useCase.ExecutarAsync(new LoginRequest("joao@email.com", "Senh@123"));

        capturado.Should().NotBeNull();
        capturado!.UsuarioId.Should().Be(_usuario.Id);
        capturado.TokenHash.Should().Be("refresh-hash");
        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task DeveLancarDomainAuthExceptionQuandoEmailNaoExiste()
    {
        _usuarioRepositoryMock
            .Setup(r => r.ObterPorEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        Func<Task> acao = () =>
            _useCase.ExecutarAsync(new LoginRequest("nao-existe@email.com", "Senh@123"));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemEsperada);
        _refreshRepositoryMock.Verify(
            r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeveLancarDomainAuthExceptionQuandoUsuarioInativo()
    {
        _usuario.Desativar();
        _usuarioRepositoryMock
            .Setup(r => r.ObterPorEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_usuario);

        Func<Task> acao = () =>
            _useCase.ExecutarAsync(new LoginRequest("joao@email.com", "Senh@123"));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemEsperada);
    }

    [Fact]
    public async Task DeveLancarDomainAuthExceptionQuandoSenhaIncorreta()
    {
        _usuarioRepositoryMock
            .Setup(r => r.ObterPorEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(false);

        Func<Task> acao = () =>
            _useCase.ExecutarAsync(new LoginRequest("joao@email.com", "errada"));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemEsperada);
    }

    [Fact]
    public async Task DeveLancarDomainAuthExceptionQuandoEmailMalFormatado()
    {
        Func<Task> acao = () =>
            _useCase.ExecutarAsync(new LoginRequest("nao-eh-email", "Senh@123"));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemEsperada);
    }

    [Fact]
    public async Task DeveLancarDomainAuthExceptionQuandoEmailVazio()
    {
        Func<Task> acao = () => _useCase.ExecutarAsync(new LoginRequest(string.Empty, "Senh@123"));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemEsperada);
    }

    [Fact]
    public async Task DeveChamarGerarAccessTokenComUsuarioCorreto()
    {
        ConfigurarLoginValido();

        await _useCase.ExecutarAsync(new LoginRequest("joao@email.com", "Senh@123"));

        _jwtTokenServiceMock.Verify(s => s.GerarAccessToken(_usuario), Times.Once);
        _jwtTokenServiceMock.Verify(s => s.GerarRefreshToken(), Times.Once);
    }

    private void ConfigurarLoginValido()
    {
        _usuarioRepositoryMock
            .Setup(r => r.ObterPorEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_usuario);
        _senhaServiceMock
            .Setup(s => s.VerificarSenha(It.IsAny<string>(), It.IsAny<SenhaHash>()))
            .Returns(true);
    }
}
