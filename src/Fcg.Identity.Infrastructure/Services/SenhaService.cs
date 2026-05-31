using Fcg.Identity.Application.Interfaces;
using Fcg.Identity.Domain.ValueObjects;

namespace Fcg.Identity.Infrastructure.Services;

public class SenhaService : ISenhaService
{
    public SenhaHash GerarHash(string senha) =>
        SenhaHash.Criar(BCrypt.Net.BCrypt.HashPassword(senha));

    public bool VerificarSenha(string senha, SenhaHash hash) =>
        BCrypt.Net.BCrypt.Verify(senha, hash.Valor);
}
