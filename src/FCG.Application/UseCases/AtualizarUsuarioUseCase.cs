using FCG.Application.DTOs;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;

namespace FCG.Application.UseCases;

public class AtualizarUsuarioUseCase(
    IUsuarioRepository repositorio,
    IUsuarioDomainService domainService,
    IUnitOfWork unitOfWork
)
{
    public async Task<UsuarioResponse?> ExecutarAsync(
        Guid id,
        AtualizarUsuarioRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var usuario = await repositorio.ObterPorIdAsync(id, cancellationToken);
        if (usuario is null)
            return null;

        var novoEmail = Email.Criar(request.Email);

        await domainService.AtualizarDadosAsync(
            usuario,
            request.Nome,
            novoEmail,
            cancellationToken
        );

        repositorio.Atualizar(usuario);
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
