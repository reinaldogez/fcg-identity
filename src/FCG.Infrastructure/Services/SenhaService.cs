using FCG.Application.Interfaces;
using FCG.Domain.ValueObjects;

namespace FCG.Infrastructure.Services;

public class SenhaService : ISenhaService
{
    public SenhaHash GerarHash(string senha)
    {
        return SenhaHash.Criar(BCrypt.Net.BCrypt.HashPassword(senha));
    }

    public bool VerificarSenha(string senha, SenhaHash hash)
    {
        return BCrypt.Net.BCrypt.Verify(senha, hash.Valor);
    }
}
