using FCG.Application.DTOs;
using FCG.Application.UseCases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace FCG.API.Controllers;

[ApiController]
[Route("api/usuarios")]
[EnableRateLimiting("fixed")]
public class UsuarioController(
    CadastrarUsuarioUseCase cadastrarUsuarioUseCase,
    ObterUsuarioPorIdUseCase obterUsuarioPorIdUseCase,
    ListarUsuariosUseCase listarUsuariosUseCase,
    AtualizarUsuarioUseCase atualizarUsuarioUseCase,
    AlterarSenhaUseCase alterarSenhaUseCase,
    DesativarUsuarioUseCase desativarUsuarioUseCase,
    AlterarTipoUsuarioUseCase alterarTipoUsuarioUseCase) : ControllerBase
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

    /// <summary>
    /// Lista usuários de forma paginada.
    /// </summary>
    /// <param name="pagina">Número da página (mínimo: 1).</param>
    /// <param name="tamanhoPagina">Quantidade de itens por página (1 a 100).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Lista paginada de usuários.</response>
    /// <response code="400">Parâmetros de paginação inválidos.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    // TODO: [Authorize(Roles = "Administrador")] — aguardando feature JWT (ver docs/debitos-tecnicos.md)
    [HttpGet]
    [ProducesResponseType(typeof(ListarUsuariosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery][Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a 1.")] int pagina = 1,
        [FromQuery][Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")] int tamanhoPagina = 20,
        CancellationToken cancellationToken = default)
    {
        var resposta = await listarUsuariosUseCase.ExecutarAsync(pagina, tamanhoPagina, cancellationToken);
        return Ok(resposta);
    }

    /// <summary>
    /// Atualiza o nome e o e-mail de um usuário.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="request">Novos dados: nome e e-mail.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Usuário atualizado com sucesso.</response>
    /// <response code="400">Dados inválidos (e-mail mal formatado ou nome vazio).</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="409">E-mail já cadastrado por outro usuário.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    // TODO: [Authorize] — apenas o próprio usuário ou Administrador; aguardando feature JWT (ver docs/debitos-tecnicos.md)
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AtualizarAsync(
        Guid id,
        [FromBody] AtualizarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var resposta = await atualizarUsuarioUseCase.ExecutarAsync(id, request, cancellationToken);
        if (resposta is null)
            return NotFound();

        return Ok(resposta);
    }

    /// <summary>
    /// Altera a senha de um usuário.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="request">Senha atual e nova senha.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="204">Senha alterada com sucesso.</response>
    /// <response code="400">Senha atual incorreta ou nova senha não atende aos requisitos.</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    // TODO: [Authorize] — apenas o próprio usuário; aguardando feature JWT (ver docs/debitos-tecnicos.md)
    [HttpPost("{id:guid}/alterar-senha")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AlterarSenhaAsync(
        Guid id,
        [FromBody] AlterarSenhaRequest request,
        CancellationToken cancellationToken)
    {
        bool encontrado = await alterarSenhaUseCase.ExecutarAsync(id, request, cancellationToken);
        if (!encontrado)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Desativa um usuário (soft delete). Operação idempotente.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="204">Usuário desativado (ou já estava desativado).</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    // TODO: [Authorize(Roles = "Administrador")] — aguardando feature JWT (ver docs/debitos-tecnicos.md)
    [HttpPatch("{id:guid}/desativar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DesativarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        bool encontrado = await desativarUsuarioUseCase.ExecutarAsync(id, cancellationToken);
        if (!encontrado)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Altera o tipo (perfil) de um usuário.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="request">Novo tipo: "Usuario" ou "Administrador".</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Tipo alterado com sucesso.</response>
    /// <response code="400">Tipo inválido.</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    // TODO: [Authorize(Roles = "Administrador")] — aguardando feature JWT (ver docs/debitos-tecnicos.md)
    [HttpPatch("{id:guid}/tipo")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AlterarTipoAsync(
        Guid id,
        [FromBody] AlterarTipoRequest request,
        CancellationToken cancellationToken)
    {
        var resposta = await alterarTipoUsuarioUseCase.ExecutarAsync(id, request, cancellationToken);
        if (resposta is null)
            return NotFound();

        return Ok(resposta);
    }
}
