using System.ComponentModel.DataAnnotations;

namespace Fcg.Identity.Application.DTOs;

public record LoginRequest([Required] string Email, [Required] string Senha);
