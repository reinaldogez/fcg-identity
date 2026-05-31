using System.Text.RegularExpressions;
using Fcg.Identity.Domain.Exceptions;

namespace Fcg.Identity.Domain.ValueObjects;

public record Email
{
    private static readonly Regex _formatoValido = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    private Email(string endereco) => Endereco = endereco;

    public string Endereco { get; }

    public static Email Criar(string endereco)
    {
        if (string.IsNullOrWhiteSpace(endereco))
        {
            throw new DomainException("O e-mail é obrigatório.");
        }

#pragma warning disable CA1308 // e-mail: normalização para lowercase é o padrão correto
        endereco = endereco.Trim().ToLowerInvariant();
#pragma warning restore CA1308

        if (!_formatoValido.IsMatch(endereco))
        {
            throw new DomainException("O formato do e-mail é inválido.");
        }

        return new Email(endereco);
    }

    public static Email Reconstituir(string endereco) => new(endereco);
}
