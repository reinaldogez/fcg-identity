using System.Text.RegularExpressions;
using FCG.Domain.Exceptions;

namespace FCG.Domain.ValueObjects;

public partial record Senha
{
    private Senha(string texto)
    {
        Texto = texto;
    }

    public string Texto { get; }

    public static Senha Validar(string senhaTexto)
    {
        if (string.IsNullOrWhiteSpace(senhaTexto))
        {
            throw new DomainException("A senha é obrigatória.");
        }

        if (senhaTexto.Length < 8)
        {
            throw new DomainException("A senha deve ter no mínimo 8 caracteres.");
        }

        if (!LetraRegex().IsMatch(senhaTexto))
        {
            throw new DomainException("A senha deve conter pelo menos uma letra.");
        }

        if (!NumeroRegex().IsMatch(senhaTexto))
        {
            throw new DomainException("A senha deve conter pelo menos um número.");
        }

        if (!CaractereEspecialRegex().IsMatch(senhaTexto))
        {
            throw new DomainException("A senha deve conter pelo menos um caractere especial.");
        }

        return new Senha(senhaTexto);
    }

    [GeneratedRegex(@"[a-zA-Z]")]
    private static partial Regex LetraRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex NumeroRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex CaractereEspecialRegex();
}
