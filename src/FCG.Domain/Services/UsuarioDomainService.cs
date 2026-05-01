using FCG.Domain.Entities;
using FCG.Domain.Exceptions;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;

namespace FCG.Domain.Services;

public class UsuarioDomainService(IUsuarioRepository repositorio) : IUsuarioDomainService
{
    public async Task<Usuario> RegistrarAsync(
        string nome,
        Email email,
        SenhaHash senhaHash,
        CancellationToken cancellationToken = default
    )
    {
        if (await repositorio.ExisteComEmailAsync(email, cancellationToken))
        {
            throw new DomainConflictException("Já existe um usuário cadastrado com este e-mail.");
        }

        return Usuario.Criar(nome, email, senhaHash);
    }

    public async Task AtualizarDadosAsync(
        Usuario usuario,
        string novoNome,
        Email novoEmail,
        CancellationToken cancellationToken = default
    )
    {
        if (
            usuario.Email != novoEmail
            && await repositorio.ExisteComEmailAsync(novoEmail, cancellationToken)
        )
        {
            throw new DomainConflictException("Já existe um usuário cadastrado com este e-mail.");
        }

        usuario.AlterarDados(novoNome, novoEmail);
    }
}
