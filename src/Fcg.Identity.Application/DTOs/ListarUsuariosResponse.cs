namespace Fcg.Identity.Application.DTOs;

public record ListarUsuariosResponse(
    IReadOnlyList<UsuarioResponse> Items,
    int Total,
    int Pagina,
    int TamanhoPagina
);
