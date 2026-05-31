using Fcg.Identity.Domain.ValueObjects;

namespace Fcg.Identity.Application.Interfaces;

public interface ISenhaService
{
    SenhaHash GerarHash(string senha);
    bool VerificarSenha(string senha, SenhaHash hash);
}
