using FCG.Domain.Exceptions;

namespace FCG.Domain.ValueObjects;

public record SenhaHash
{
    public string Valor { get; }

    private SenhaHash(string valor)
    {
        Valor = valor;
    }

    public static SenhaHash Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException("O hash da senha é obrigatório.");
        }

        return new SenhaHash(valor);
    }

    public static SenhaHash Reconstituir(string valor) => new(valor);
}
