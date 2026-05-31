using System.ComponentModel.DataAnnotations;

namespace Fcg.Identity.Application.DTOs;

public record LogoutRequest([Required] string RefreshToken);
