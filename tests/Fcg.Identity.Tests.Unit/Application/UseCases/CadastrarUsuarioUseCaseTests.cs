using Fcg.Contracts.Events;
using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Application.UseCases;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Enums;
using Fcg.Identity.Domain.Exceptions;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.ValueObjects;
using FluentAssertions;
using MassTransit;
using Moq;

namespace Fcg.Identity.Tests.Unit.Application.UseCases;

public class CadastrarUsuarioUseCaseTests
{
    private readonly Mock<IUsuarioDomainService> _domainServiceMock = new();
    private readonly Mock<IUsuarioRepository> _repositorioMock = new();
    private readonly Mock<ISenhaService> _senhaServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPublishEndpoint> _publishEndpointMock = new();
    private readonly CadastrarUsuarioUseCase _useCase;

    public CadastrarUsuarioUseCaseTests()
    {
        _senhaServiceMock
            .Setup(s => s.GerarHash(It.IsAny<string>()))
            .Returns(SenhaHash.Reconstituir("hash-gerado"));

        _domainServiceMock
            .Setup(s =>
                s.RegistrarAsync(
                    It.IsAny<string>(),
                    It.IsAny<Email>(),
                    It.IsAny<SenhaHash>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (string nome, Email email, SenhaHash hash, CancellationToken _) =>
                    Usuario.Criar(nome, email, hash)
            );

        _useCase = new CadastrarUsuarioUseCase(
            _domainServiceMock.Object,
            _repositorioMock.Object,
            _senhaServiceMock.Object,
            _unitOfWorkMock.Object,
            _publishEndpointMock.Object
        );
    }

    [Fact]
    public async Task DeveCadastrarUsuarioComSucesso()
    {
        var request = new CadastrarUsuarioRequest("João Silva", "joao@email.com", "Senh@123");

        UsuarioResponse resultado = await _useCase.ExecutarAsync(request);

        resultado.Nome.Should().Be("João Silva");
        resultado.Email.Should().Be("joao@email.com");
        resultado.Tipo.Should().Be(TipoUsuario.Usuario.ToString());
        resultado.Ativo.Should().BeTrue();
        resultado.Id.Should().NotBe(Guid.Empty);

        _repositorioMock.Verify(
            r => r.AdicionarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task DeveRejeitarEmailDuplicado()
    {
        _domainServiceMock
            .Setup(s =>
                s.RegistrarAsync(
                    It.IsAny<string>(),
                    It.IsAny<Email>(),
                    It.IsAny<SenhaHash>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new DomainConflictException("Já existe um usuário cadastrado com este e-mail.")
            );

        var request = new CadastrarUsuarioRequest("João Silva", "joao@email.com", "Senh@123");

        Func<Task<UsuarioResponse>> acao = () => _useCase.ExecutarAsync(request);

        await acao.Should().ThrowAsync<DomainConflictException>().WithMessage("*e-mail*");
    }

    [Fact]
    public async Task DeveRejeitarNomeVazio()
    {
        var request = new CadastrarUsuarioRequest(string.Empty, "joao@email.com", "Senh@123");

        Func<Task<UsuarioResponse>> acao = () => _useCase.ExecutarAsync(request);

        await acao.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DeveRejeitarEmailInvalido()
    {
        var request = new CadastrarUsuarioRequest("João Silva", "email-invalido", "Senh@123");

        Func<Task<UsuarioResponse>> acao = () => _useCase.ExecutarAsync(request);

        await acao.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DeveRejeitarSenhaFraca()
    {
        var request = new CadastrarUsuarioRequest("João Silva", "joao@email.com", "123");

        Func<Task<UsuarioResponse>> acao = () => _useCase.ExecutarAsync(request);

        await acao.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DeveChamarGerarHashComSenhaInformada()
    {
        var request = new CadastrarUsuarioRequest("João Silva", "joao@email.com", "Senh@123");

        await _useCase.ExecutarAsync(request);

        _senhaServiceMock.Verify(s => s.GerarHash("Senh@123"), Times.Once);
    }

    [Fact]
    public async Task DeveChamarSalvarAlteracoesAposCadastro()
    {
        var request = new CadastrarUsuarioRequest("João Silva", "joao@email.com", "Senh@123");

        await _useCase.ExecutarAsync(request);

        _unitOfWorkMock.Verify(
            u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task DevePublicarUserCreatedEventComDadosDoUsuario()
    {
        UserCreatedEvent? eventoPublicado = null;
        _publishEndpointMock
            .Setup(p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<UserCreatedEvent, CancellationToken>((e, _) => eventoPublicado = e)
            .Returns(Task.CompletedTask);

        var request = new CadastrarUsuarioRequest("João Silva", "joao@email.com", "Senh@123");

        UsuarioResponse resultado = await _useCase.ExecutarAsync(request);

        _publishEndpointMock.Verify(
            p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        eventoPublicado.Should().NotBeNull();
        eventoPublicado!.EventVersion.Should().Be(1);
        eventoPublicado.UserId.Should().Be(resultado.Id);
        eventoPublicado.Name.Should().Be("João Silva");
        eventoPublicado.Email.Should().Be("joao@email.com");
        eventoPublicado
            .OccurredAt.Should()
            .BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DevePublicarAntesDeSalvarAlteracoes()
    {
        var ordemDasChamadas = new List<string>();
        _publishEndpointMock
            .Setup(p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => ordemDasChamadas.Add("Publish"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => ordemDasChamadas.Add("SalvarAlteracoes"))
            .Returns(Task.CompletedTask);

        var request = new CadastrarUsuarioRequest("João Silva", "joao@email.com", "Senh@123");

        await _useCase.ExecutarAsync(request);

        // O publish precisa anteceder o commit para a mensagem entrar na mesma transação do usuário.
        ordemDasChamadas.Should().Equal("Publish", "SalvarAlteracoes");
    }

    [Fact]
    public async Task NaoDevePublicarEventoQuandoCadastroFalha()
    {
        _domainServiceMock
            .Setup(s =>
                s.RegistrarAsync(
                    It.IsAny<string>(),
                    It.IsAny<Email>(),
                    It.IsAny<SenhaHash>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new DomainConflictException("Já existe um usuário cadastrado com este e-mail.")
            );

        var request = new CadastrarUsuarioRequest("João Silva", "joao@email.com", "Senh@123");

        Func<Task<UsuarioResponse>> acao = () => _useCase.ExecutarAsync(request);

        await acao.Should().ThrowAsync<DomainConflictException>();
        _publishEndpointMock.Verify(
            p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
