using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Application.UseCases;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace Fcg.Identity.Tests.Unit.Application.UseCases;

public class LogoutUseCaseTests
{
    private const string Plaintext = "refresh-plain";
    private const string Hash = "refresh-hash";

    private readonly Mock<IRefreshTokenRepository> _refreshRepoMock = new();
    private readonly Mock<IJwtTokenService> _jwtMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly LogoutUseCase _useCase;

    public LogoutUseCaseTests()
    {
        _jwtMock.Setup(j => j.CalcularHashRefreshToken(Plaintext)).Returns(Hash);

        _useCase = new LogoutUseCase(_refreshRepoMock.Object, _jwtMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task DeveRevogarTokenExistenteAtivoEPersistir()
    {
        var token = RefreshToken.Criar(Guid.NewGuid(), Hash, DateTime.UtcNow.AddDays(7));
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        await _useCase.ExecutarAsync(new LogoutRequest(Plaintext));

        token.RevogadoEm.Should().NotBeNull();
        _uowMock.Verify(u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeveSerNoOpQuandoTokenInexistente()
    {
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        await _useCase.ExecutarAsync(new LogoutRequest(Plaintext));

        _uowMock.Verify(u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeveSerNoOpQuandoTokenJaRevogado()
    {
        var token = RefreshToken.Criar(Guid.NewGuid(), Hash, DateTime.UtcNow.AddDays(7));
        token.Revogar();
        _refreshRepoMock
            .Setup(r => r.ObterPorHashAsync(Hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        await _useCase.ExecutarAsync(new LogoutRequest(Plaintext));

        _uowMock.Verify(u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
