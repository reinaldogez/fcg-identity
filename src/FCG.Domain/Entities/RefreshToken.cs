using FCG.Domain.Exceptions;

namespace FCG.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiraEm { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? RevogadoEm { get; private set; }
    public Guid? SubstituidoPor { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Criar(Guid usuarioId, string tokenHash, DateTime expiraEm)
    {
        if (usuarioId == Guid.Empty)
            throw new DomainException("O usuário do refresh token é obrigatório.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("O hash do refresh token é obrigatório.");

        if (expiraEm <= DateTime.UtcNow)
            throw new DomainException("A expiração do refresh token deve ser futura.");

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            TokenHash = tokenHash,
            ExpiraEm = expiraEm,
            CriadoEm = DateTime.UtcNow,
            RevogadoEm = null,
            SubstituidoPor = null
        };
    }

    public bool EstaAtivo => RevogadoEm is null && DateTime.UtcNow < ExpiraEm;

    public void Revogar()
    {
        if (RevogadoEm is not null)
            return;

        RevogadoEm = DateTime.UtcNow;
    }

    public void RevogarESubstituirPor(Guid novoTokenId)
    {
        if (RevogadoEm is not null)
            throw new DomainException("Refresh token já foi revogado.");

        if (novoTokenId == Guid.Empty)
            throw new DomainException("O identificador do token substituto é obrigatório.");

        RevogadoEm = DateTime.UtcNow;
        SubstituidoPor = novoTokenId;
    }
}
