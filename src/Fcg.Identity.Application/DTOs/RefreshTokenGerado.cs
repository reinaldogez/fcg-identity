namespace Fcg.Identity.Application.DTOs;

public sealed record RefreshTokenGerado(string Plaintext, string Hash, DateTime ExpiraEm);
