using FCG.Application.DTOs;
using FCG.Application.UseCases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FCG.API.Controllers;

[ApiController]
[Route("api/usuarios")]
[EnableRateLimiting("fixed")]
public class UsuarioController(
    CadastrarUsuarioUseCase cadastrarUsuarioUseCase,
    ObterUsuarioPorIdUseCase obterUsuarioPorIdUseCase) : ControllerBase
{
    /// <summary>
    /// Cadastra um novo usuário na plataforma.
    /// </summary>
    /// <param name="request">Dados do usuário: nome, e-mail e senha.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="201">Usuário cadastrado com sucesso. O header Location aponta para o recurso criado.</response>
    /// <response code="400">Dados inválidos (e-mail mal formatado, senha fraca ou nome vazio).</response>
    /// <response code="409">E-mail já cadastrado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPost]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CadastrarAsync(
        [FromBody] CadastrarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var resposta = await cadastrarUsuarioUseCase.ExecutarAsync(request, cancellationToken);
        return CreatedAtRoute("ObterUsuarioPorId", new { id = resposta.Id }, resposta);
    }

    /// <summary>
    /// Obtém os dados de um usuário pelo seu identificador.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Usuário encontrado.</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpGet("{id:guid}", Name = "ObterUsuarioPorId")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resposta = await obterUsuarioPorIdUseCase.ExecutarAsync(id, cancellationToken);

        if (resposta is null)
            return NotFound();

        return Ok(resposta);
    }
}
