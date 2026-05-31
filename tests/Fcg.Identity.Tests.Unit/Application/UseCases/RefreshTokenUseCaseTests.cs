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

public class RefreshTokenUseCaseTests
{
    private const string MensagemFalha = "Refresh token inválido.";
    private const string PlaintextEntrada = "refresh-plain";
    private const string HashEntrada = "hash-plain";
    private const string NovoPlaintext = "novo-plain";
    private const string NovoHash = "novo-hash";

    private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshRepoMock = new();
    private readonly Mock<IJwtTokenService> _jwtMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly RefreshTokenUseCase _useCase;
    private readonly Usuario _usuario;
    private readonly RefreshToken _tokenAtivo;

    public RefreshTokenUseCaseTests()
    {
        _usuario = Usuario.Criar(
            "João",
            Email.Criar("joao@email.com"),
            SenhaHash.Reconstituir("$2a$11$hash")
        );
        _tokenAtivo = RefreshToken.Criar(_usuario.Id, HashEntrada, DateTime.UtcNow.AddDays(7));

        _jwtMock.Setup(j => j.CalcularHashRefreshToken(PlaintextEntrada)).Returns(HashEntrada);
        _jwtMock
            .Setup(j => j.GerarAccessToken(It.IsAny<Usuario>()))
            .Returns(new AccessToken("access-token", DateTime.UtcNow.AddHours(1), 3600));
        _jwtMock
            .Setup(j => j.GerarRefreshToken())
            .Returns(new RefreshTokenGerado(NovoPlaintext, NovoHash, DateTime.UtcNow.AddDays(7)));

        _useCase = new RefreshTokenUseCase(
            _usuarioRepoMock.Object,
            _refreshRepoMock.Object,
            _jwtMock.Object,
            _uowMock.Object
        );
    }

    [Fact]
    public async Task DeveEmitirNovoParEAplicarRotacaoQuandoRefreshValido()
    {
        ConfigurarFluxoValido();
        RefreshToken? novoSalvo = null;
        _refreshRepoMock
            .Setup(r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((rt, _) => novoSalvo = rt)
            .Returns(Task.CompletedTask);

        LoginResponse resposta = await _useCase.ExecutarAsync(
            new RefreshTokenRequest(PlaintextEntrada)
        );

        resposta.AccessToken.Should().Be("access-token");
        resposta.RefreshToken.Should().Be(NovoPlaintext);
        resposta.TokenType.Should().Be("Bearer");

        _tokenAtivo.RevogadoEm.Should().NotBeNull();
        _tokenAtivo.SubstituidoPor.Should().NotBeNull();
        novoSalvo.Should().NotBeNull();
        _tokenAtivo.SubstituidoPor.Should().Be(novoSalvo!.Id);

        _uowMock.Verify(u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeveLancarQuandoTokenInexistente()
    {
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(HashEntrada, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        Func<Task> acao = () => _useCase.ExecutarAsync(new RefreshTokenRequest(PlaintextEntrada));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemFalha);
    }

    [Fact]
    public async Task DeveLancarQuandoTokenJaRevogado()
    {
        _tokenAtivo.Revogar();
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(HashEntrada, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tokenAtivo);

        Func<Task> acao = () => _useCase.ExecutarAsync(new RefreshTokenRequest(PlaintextEntrada));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemFalha);
        _refreshRepoMock.Verify(
            r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeveLancarQuandoTokenExpirado()
    {
        var expirado = RefreshToken.Criar(
            _usuario.Id,
            HashEntrada,
            DateTime.UtcNow.AddMilliseconds(50)
        );
        await Task.Delay(100);
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(HashEntrada, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expirado);

        Func<Task> acao = () => _useCase.ExecutarAsync(new RefreshTokenRequest(PlaintextEntrada));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemFalha);
    }

    [Fact]
    public async Task DeveLancarQuandoUsuarioNaoExisteMais()
    {
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(HashEntrada, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tokenAtivo);
        _usuarioRepoMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        Func<Task> acao = () => _useCase.ExecutarAsync(new RefreshTokenRequest(PlaintextEntrada));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemFalha);
    }

    [Fact]
    public async Task DeveLancarQuandoUsuarioInativo()
    {
        _usuario.Desativar();
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(HashEntrada, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tokenAtivo);
        _usuarioRepoMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_usuario);

        Func<Task> acao = () => _useCase.ExecutarAsync(new RefreshTokenRequest(PlaintextEntrada));

        await acao.Should().ThrowAsync<DomainAuthException>().WithMessage(MensagemFalha);
    }

    private void ConfigurarFluxoValido()
    {
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(HashEntrada, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tokenAtivo);
        _usuarioRepoMock
            .Setup(r => r.ObterPorIdAsync(_usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_usuario);
    }
}
