namespace FCG.Application.DTOs;

public sealed record AccessToken(string Token, DateTime ExpiraEm, int ExpiresInSeconds);
