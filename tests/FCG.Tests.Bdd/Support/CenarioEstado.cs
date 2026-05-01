namespace FCG.Tests.Bdd.Support;

public class CenarioEstado
{
    public HttpResponseMessage? UltimaResposta { get; set; }
    public string? TokenAcesso { get; set; }
    public string? RefreshToken { get; set; }
    public string? RefreshTokenAnterior { get; set; }
}
