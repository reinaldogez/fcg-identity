using System.Text.RegularExpressions;
using FCG.Domain.Exceptions;

namespace FCG.Domain.ValueObjects;

public record Email
{
    private static readonly Regex FormatoValido = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled);

    public string Endereco { get; }

    private Email(string endereco)
    {
        Endereco = endereco;
    }

    public static Email Criar(string endereco)
    {
        if (string.IsNullOrWhiteSpace(endereco))
        {
            throw new DomainException("O e-mail é obrigatório.");
        }

        endereco = endereco.Trim().ToLowerInvariant();

        if (!FormatoValido.IsMatch(endereco))
        {
            throw new DomainException("O formato do e-mail é inválido.");
        }

        return new Email(endereco);
    }

    public static Email Reconstituir(string endereco) => new(endereco);
}
