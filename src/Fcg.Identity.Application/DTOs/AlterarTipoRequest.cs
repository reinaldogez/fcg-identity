using System.ComponentModel.DataAnnotations;
using Fcg.Identity.Domain.Enums;

namespace Fcg.Identity.Application.DTOs;

public record AlterarTipoRequest([Required] TipoUsuario Tipo);
