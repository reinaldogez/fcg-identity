using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.UseCases;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Exceptions;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Fcg.Identity.Tests.Unit.Application.UseCases;

public class AtualizarUsuarioUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly Mock<IUsuarioDomainService> _domainServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AtualizarUsuarioUseCase _useCase;

    private readonly Email _emailValido = Email.Criar("original@email.com");
    private readonly SenhaHash _senhaHashValida = SenhaHash.Reconstituir("$2a$11$hashFalso");

    public AtualizarUsuarioUseCaseTests()
    {
        _useCase = new AtualizarUsuarioUseCase(
            _repositorioMock.Object,
            _domainServiceMock.Object,
            _unitOfWorkMock.Object
        );
    }

    [Fact]
    public async Task DeveAtualizarUsuarioComSucesso()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _domainServiceMock
            .Setup(s =>
                s.AtualizarDadosAsync(
                    usuario,
                    "Novo Nome",
                    It.IsAny<Email>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Usuario, string, Email, CancellationToken>(
                (u, nome, email, _) => u.AlterarDados(nome, email)
            );

        var request = new AtualizarUsuarioRequest("Novo Nome", "novo@email.com");

        UsuarioResponse? resultado = await _useCase.ExecutarAsync(usuario.Id, request);

        resultado.Should().NotBeNull();
        resultado!.Nome.Should().Be("Novo Nome");
    }

    [Fact]
    public async Task DeveRetornarNullQuandoUsuarioNaoEncontrado()
    {
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        UsuarioResponse? resultado = await _useCase.ExecutarAsync(
            Guid.NewGuid(),
            new AtualizarUsuarioRequest("Nome", "email@email.com")
        );

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task DeveRejeitarEmailInvalido()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        Func<Task<UsuarioResponse?>> acao = () =>
            _useCase.ExecutarAsync(
                usuario.Id,
                new AtualizarUsuarioRequest("Nome", "email-invalido")
            );

        await acao.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DevePropagarDomainConflictExceptionQuandoEmailDuplicado()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _domainServiceMock
            .Setup(s =>
                s.AtualizarDadosAsync(
                    It.IsAny<Usuario>(),
                    It.IsAny<string>(),
                    It.IsAny<Email>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new DomainConflictException("Já existe um usuário cadastrado com este e-mail.")
            );

        Func<Task<UsuarioResponse?>> acao = () =>
            _useCase.ExecutarAsync(
                usuario.Id,
                new AtualizarUsuarioRequest("Nome", "outro@email.com")
            );

        await acao.Should().ThrowAsync<DomainConflictException>().WithMessage("*e-mail*");
    }

    [Fact]
    public async Task DeveChamarRepositorioAtualizar()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _domainServiceMock
            .Setup(s =>
                s.AtualizarDadosAsync(
                    It.IsAny<Usuario>(),
                    It.IsAny<string>(),
                    It.IsAny<Email>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        await _useCase.ExecutarAsync(
            usuario.Id,
            new AtualizarUsuarioRequest("Nome", "original@email.com")
        );

        _repositorioMock.Verify(r => r.Atualizar(usuario), Times.Once);
    }

    [Fact]
    public async Task DeveChamarSalvarAlteracoes()
    {
        Usuario usuario = CriarUsuario();
        _repositorioMock
            .Setup(r => r.ObterPorIdAsync(usuario.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _domainServiceMock
            .Setup(s =>
                s.AtualizarDadosAsync(
                    It.IsAny<Usuario>(),
                    It.IsAny<string>(),
                    It.IsAny<Email>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        await _useCase.ExecutarAsync(
            usuario.Id,
            new AtualizarUsuarioRequest("Nome", "original@email.com")
        );

        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private Usuario CriarUsuario() =>
        Usuario.Criar("Nome Original", _emailValido, _senhaHashValida);
}
