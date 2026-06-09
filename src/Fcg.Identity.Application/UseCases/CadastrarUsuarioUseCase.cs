using Fcg.Contracts.Events;
using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.ValueObjects;
using MassTransit;

namespace Fcg.Identity.Application.UseCases;

public class CadastrarUsuarioUseCase(
    IUsuarioDomainService usuarioDomainService,
    IUsuarioRepository repositorio,
    ISenhaService senhaService,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint
)
{
    public async Task<UsuarioResponse> ExecutarAsync(
        CadastrarUsuarioRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var email = Email.Criar(request.Email);
        var senha = Senha.Validar(request.Senha);
        SenhaHash senhaHash = senhaService.GerarHash(senha.Texto);

        Usuario usuario = await usuarioDomainService.RegistrarAsync(
            request.Nome,
            email,
            senhaHash,
            cancellationToken
        );

        await repositorio.AdicionarAsync(usuario, cancellationToken);

        // O publish antes do SalvarAlteracoesAsync é intencional: o Outbox do bus intercepta a
        // chamada e grava a mensagem como linha de OutboxMessage no mesmo commit do Usuario —
        // atômico. A entrega ao broker acontece em background, depois do commit.
        await publishEndpoint.Publish(
            new UserCreatedEvent
            {
                EventVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                UserId = usuario.Id,
                Name = usuario.Nome,
                Email = usuario.Email.Endereco,
            },
            cancellationToken
        );

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return new UsuarioResponse(
            usuario.Id,
            usuario.Nome,
            usuario.Email.Endereco,
            usuario.Tipo.ToString(),
            usuario.DataCriacao,
            usuario.Ativo
        );
    }
}
