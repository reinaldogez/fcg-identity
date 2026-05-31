namespace Fcg.Identity.Application.DTOs;

public record UsuarioResponse(
    Guid Id,
    string Nome,
    string Email,
    string Tipo,
    DateTime DataCriacao,
    bool Ativo
);
