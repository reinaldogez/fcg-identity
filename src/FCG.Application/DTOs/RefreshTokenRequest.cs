using System.ComponentModel.DataAnnotations;

namespace FCG.Application.DTOs;

public record RefreshTokenRequest([Required] string RefreshToken);
