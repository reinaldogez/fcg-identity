using System.ComponentModel.DataAnnotations;

namespace FCG.Application.DTOs;

public record LogoutRequest([Required] string RefreshToken);
