using FCG.Domain.ValueObjects;

namespace FCG.Application.Interfaces;

public interface ISenhaService
{
    SenhaHash GerarHash(string senha);
    bool VerificarSenha(string senha, SenhaHash hash);
}
