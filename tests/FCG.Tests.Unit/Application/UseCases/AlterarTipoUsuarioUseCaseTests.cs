using FCG.Application.DTOs;
using FCG.Application.UseCases;
using FCG.Domain.Entities;
using FCG.Domain.Enums;
using FCG.Domain.Exceptions;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace FCG.Tests.Unit.Application.UseCases;

public class AlterarTipoUsuarioUseCaseTests
{
    private static readonly Guid SolicitanteAdminId = Guid.NewGuid();

    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AlterarTipoUsuarioUseCase _useCase;

    public AlterarTipoUsuarioUseCaseTests()
    {
        _useCase = new AlterarTipoUsuarioUseCase(_repositorioMock.Object, _unitOfWorkMock.Object);
    }

    private static Usuario CriarUsuario(TipoUsuario tipo = TipoUsuario.Usuario) =>
        Usuario.Criar(
            "Nome",
            Email.Criar("teste@email.com"),
            SenhaHash.Reconstituir("$2a$11$hashFalso"),
            tipo);

    [Fact]
    public async Task DeveAlterarTipoParaAdministrador()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        UsuarioResponse? resultado = await _useCase.ExecutarAsync(
            usuario.Id, SolicitanteAdminId, new AlterarTipoRequest(TipoUsuario.Administrador));

        resultado.Should().NotBeNull();
        resultado!.Tipo.Should().Be(TipoUsuario.Administrador.ToString());
    }

    [Fact]
    public async Task DeveAlterarTipoParaUsuario()
    {
        Usuario usuario = CriarUsuario(TipoUsuario.Administrador);
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        UsuarioResponse? resultado = await _useCase.ExecutarAsync(
            usuario.Id, SolicitanteAdminId, new AlterarTipoRequest(TipoUsuario.Usuario));

        resultado!.Tipo.Should().Be(TipoUsuario.Usuario.ToString());
    }

    [Fact]
    public async Task DeveRetornarNullQuandoUsuarioNaoEncontrado()
    {
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        UsuarioResponse? resultado = await _useCase.ExecutarAsync(
            Guid.NewGuid(), SolicitanteAdminId, new AlterarTipoRequest(TipoUsuario.Administrador));

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task DeveChamarSalvarAlteracoes()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        await _useCase.ExecutarAsync(usuario.Id, SolicitanteAdminId, new AlterarTipoRequest(TipoUsuario.Administrador));

        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeveLancarDomainExceptionQuandoAdminTentaRebaixarASiMesmo()
    {
        Usuario admin = CriarUsuario(TipoUsuario.Administrador);
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);

        Func<Task> acao = () => _useCase.ExecutarAsync(
            admin.Id, admin.Id, new AlterarTipoRequest(TipoUsuario.Usuario));

        await acao.Should().ThrowAsync<DomainException>()
            .WithMessage("*rebaixar a si mesmo*");
    }
}
