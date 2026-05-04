using FCG.Domain.Exceptions;
using HotChocolate;
using HotChocolate.Execution;

namespace FCG.API.GraphQL.Errors;

// Mapeia exceções de domínio para erros GraphQL preservando a mensagem em PT-BR
// e adicionando um code estável para clientes consumirem.
// Atenção: a ordem importa por causa da hierarquia de exceções.
public class DomainErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        return error.Exception switch
        {
            DomainAuthException ex => CriarErro(error, ex.Message, "ERRO_DE_AUTENTICACAO"),
            DomainConflictException ex => CriarErro(error, ex.Message, "ERRO_DE_NEGOCIO"),
            DomainException ex => CriarErro(error, ex.Message, "ERRO_DE_VALIDACAO"),
            _ => error,
        };
    }

    private static IError CriarErro(IError original, string mensagem, string code) =>
        original.WithMessage(mensagem).SetExtension("code", code);
}
