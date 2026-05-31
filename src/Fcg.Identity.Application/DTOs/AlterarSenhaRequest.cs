using System.ComponentModel.DataAnnotations;

namespace Fcg.Identity.Application.DTOs;

public record AlterarSenhaRequest([Required] string SenhaAtual, [Required] string NovaSenha);
