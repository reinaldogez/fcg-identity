using FCG.Domain.Enums;
using FCG.Domain.Exceptions;
using FCG.Domain.ValueObjects;

namespace FCG.Domain.Entities;

public class Usuario
{
    public const int NomeTamanhoMaximo = 200;

    private Usuario() { }

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public SenhaHash SenhaHash { get; private set; } = null!;
    public TipoUsuario Tipo { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public bool Ativo { get; private set; }

    public static Usuario Criar(
        string nome,
        Email email,
        SenhaHash senhaHash,
        TipoUsuario tipo = TipoUsuario.Usuario
    )
    {
        return new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = ValidarNome(nome),
            Email = email,
            SenhaHash = senhaHash,
            Tipo = tipo,
            DataCriacao = DateTime.UtcNow,
            Ativo = true,
        };
    }

    public void AlterarDados(string novoNome, Email novoEmail)
    {
        Nome = ValidarNome(novoNome);
        Email = novoEmail ?? throw new DomainException("O e-mail é obrigatório.");
    }

    public void AlterarSenha(SenhaHash novoHash)
    {
        if (novoHash is null)
            throw new DomainException("O hash da senha é obrigatório.");

        SenhaHash = novoHash;
    }

    public void Desativar()
    {
        if (!Ativo)
            return;

        Ativo = false;
    }

    public void AlterarTipo(TipoUsuario novoTipo)
    {
        if (!Enum.IsDefined(novoTipo))
            throw new DomainException("Tipo de usuário inválido.");

        Tipo = novoTipo;
    }

    public void AlterarTipoSolicitadoPor(TipoUsuario novoTipo, Guid solicitanteId)
    {
        if (!Enum.IsDefined(novoTipo))
            throw new DomainException("Tipo de usuário inválido.");

        if (
            solicitanteId == Id
            && Tipo == TipoUsuario.Administrador
            && novoTipo != TipoUsuario.Administrador
        )
        {
            throw new DomainException("Um administrador não pode rebaixar a si mesmo.");
        }

        Tipo = novoTipo;
    }

    private static string ValidarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");

        string nomeTrimmed = nome.Trim();

        if (nomeTrimmed.Length > NomeTamanhoMaximo)
            throw new DomainException($"O nome deve ter no máximo {NomeTamanhoMaximo} caracteres.");

        return nomeTrimmed;
    }
}
