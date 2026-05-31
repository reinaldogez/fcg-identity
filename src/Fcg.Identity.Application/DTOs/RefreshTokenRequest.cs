using System.ComponentModel.DataAnnotations;

namespace Fcg.Identity.Application.DTOs;

public record RefreshTokenRequest([Required] string RefreshToken);
