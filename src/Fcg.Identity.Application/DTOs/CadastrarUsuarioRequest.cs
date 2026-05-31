using System.ComponentModel.DataAnnotations;

namespace Fcg.Identity.Application.DTOs;

public record CadastrarUsuarioRequest(
    [Required] string Nome,
    [Required] string Email,
    [Required] string Senha
);
