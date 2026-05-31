using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Exceptions;
using Fcg.Identity.Domain.Interfaces;
using Fcg.Identity.Domain.ValueObjects;

namespace Fcg.Identity.Application.UseCases;

public class AlterarSenhaUseCase(
    IUsuarioRepository repositorio,
    ISenhaService senhaService,
    IUnitOfWork unitOfWork
)
{
    public async Task<bool> ExecutarAsync(
        Guid id,
        AlterarSenhaRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Usuario? usuario = await repositorio.ObterPorIdAsync(id, cancellationToken);
        if (usuario is null)
            return false;

        if (!senhaService.VerificarSenha(request.SenhaAtual, usuario.SenhaHash))
            throw new DomainException("A senha atual informada está incorreta.");

        var novaSenha = Senha.Validar(request.NovaSenha);
        SenhaHash novoHash = senhaService.GerarHash(novaSenha.Texto);

        usuario.AlterarSenha(novoHash);
        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return true;
    }
}
