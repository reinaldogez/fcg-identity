using System.ComponentModel.DataAnnotations;

namespace Fcg.Identity.Application.DTOs;

public record AtualizarUsuarioRequest([Required] string Nome, [Required] string Email);
