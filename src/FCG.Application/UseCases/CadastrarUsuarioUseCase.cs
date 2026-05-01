using FCG.Application.DTOs;
using FCG.Application.Interfaces;
using FCG.Domain.Entities;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;

namespace FCG.Application.UseCases;

public class CadastrarUsuarioUseCase(
    IUsuarioDomainService usuarioDomainService,
    IUsuarioRepository repositorio,
    ISenhaService senhaService,
    IUnitOfWork unitOfWork
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
