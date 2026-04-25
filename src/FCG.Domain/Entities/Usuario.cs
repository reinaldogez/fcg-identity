using FCG.Domain.Enums;
using FCG.Domain.Exceptions;
using FCG.Domain.ValueObjects;

namespace FCG.Domain.Entities;

public class Usuario
{
    public const int NomeTamanhoMaximo = 200;

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public SenhaHash SenhaHash { get; private set; } = null!;
    public TipoUsuario Tipo { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public bool Ativo { get; private set; }

    private Usuario() { }

    public static Usuario Criar(
        string nome,
        Email email,
        SenhaHash senhaHash,
        TipoUsuario tipo = TipoUsuario.Usuario)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new DomainException("O nome é obrigatório.");
        }

        var nomeTrimmed = nome.Trim();

        if (nomeTrimmed.Length > NomeTamanhoMaximo)
        {
            throw new DomainException($"O nome deve ter no máximo {NomeTamanhoMaximo} caracteres.");
        }

        return new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = nomeTrimmed,
            Email = email,
            SenhaHash = senhaHash,
            Tipo = tipo,
            DataCriacao = DateTime.UtcNow,
            Ativo = true
        };
    }
}
