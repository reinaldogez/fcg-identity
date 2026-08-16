using Fcg.Identity.Application.DTOs;
using Fcg.Identity.Application.UseCases.Relatorios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fcg.Identity.Api.Controllers;

[ApiController]
[Route("api/admin/relatorios")]
[Authorize(Roles = "Administrador")]
public class AdminRelatoriosController(ObterRelatorioUsuariosUseCase obterRelatorioUsuariosUseCase)
    : ControllerBase
{
    /// <summary>
    /// Obtém um relatório administrativo agregado de usuários.
    /// </summary>
    /// <remarks>
    /// Resposta resolvida em uma única ida ao banco via Dapper (`QueryMultipleAsync`),
    /// substituindo múltiplas consultas que seriam necessárias com EF Core para as mesmas agregações.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Relatório com agregações de usuários.</response>
    /// <response code="401">Requisição sem token ou com token inválido.</response>
    /// <response code="403">Apenas administradores podem acessar relatórios.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpGet("usuarios")]
    [ProducesResponseType(typeof(RelatorioUsuariosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ObterRelatorioUsuariosAsync(
        CancellationToken cancellationToken
    )
    {
        RelatorioUsuariosDto resposta = await obterRelatorioUsuariosUseCase.ExecutarAsync(
            cancellationToken
        );
        return Ok(resposta);
    }
}
