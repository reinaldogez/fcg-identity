namespace Fcg.Identity.Application.DTOs;

public record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string? RefreshToken = null
);
