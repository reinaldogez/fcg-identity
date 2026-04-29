using System.Text.Json;
using FCG.API.Middlewares;
using FCG.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FCG.Tests.Unit.Api.Middlewares;

public class ErrorHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task DeveMapearDomainConflictExceptionParaStatus409()
    {
        HttpContext context = await InvocarMiddlewareComExcecaoAsync(
            new DomainConflictException("E-mail já cadastrado."));

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeveMapearDomainExceptionParaStatus400()
    {
        HttpContext context = await InvocarMiddlewareComExcecaoAsync(
            new DomainException("E-mail inválido."));

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveMapearExceptionGenericaParaStatus500()
    {
        HttpContext context = await InvocarMiddlewareComExcecaoAsync(
            new InvalidOperationException("Falha inesperada."));

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task DeveCapturarDomainConflictExceptionAntesDeDomainException()
    {
        HttpContext context = await InvocarMiddlewareComExcecaoAsync(
            new DomainConflictException("Conflito."));

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.StatusCode.Should().NotBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveDefinirContentTypeProblemJson()
    {
        HttpContext context = await InvocarMiddlewareComExcecaoAsync(
            new DomainException("Erro."));

        context.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task DeveRetornarProblemDetailsComTipoErroDeValidacaoParaDomainException()
    {
        JsonElement body = await InvocarECapturarBodyAsync(
            new DomainException("E-mail inválido."));

        body.GetProperty("type").GetString().Should().Be("ErroDeValidacao");
        body.GetProperty("title").GetString().Should().Be("Erro ao processar requisição");
        body.GetProperty("status").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task DeveRetornarProblemDetailsComTipoErroDeNegocioParaDomainConflictException()
    {
        JsonElement body = await InvocarECapturarBodyAsync(
            new DomainConflictException("E-mail já cadastrado."));

        body.GetProperty("type").GetString().Should().Be("ErroDeNegocio");
        body.GetProperty("status").GetInt32().Should().Be(409);
    }

    [Fact]
    public async Task DeveRetornarProblemDetailsComTipoErroInternoParaExceptionGenerica()
    {
        JsonElement body = await InvocarECapturarBodyAsync(
            new InvalidOperationException("Falha no banco."));

        body.GetProperty("type").GetString().Should().Be("ErroInterno");
        body.GetProperty("status").GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task DeveOcultarDetalhesTecnicosParaErro500()
    {
        JsonElement body = await InvocarECapturarBodyAsync(
            new InvalidOperationException("Conexão recusada ao servidor SQL na porta 1433."));

        string mensagem = body.GetProperty("errors")[0].GetString()!;
        mensagem.Should().Be("Ocorreu um erro interno no servidor.");
        mensagem.Should().NotContain("SQL");
        mensagem.Should().NotContain("1433");
    }

    [Fact]
    public async Task DeveExporMensagemOriginalDeDomainException()
    {
        JsonElement body = await InvocarECapturarBodyAsync(
            new DomainException("E-mail inválido."));

        body.GetProperty("errors")[0].GetString().Should().Be("E-mail inválido.");
    }

    [Fact]
    public async Task DeveIncluirTraceIdNaResposta()
    {
        JsonElement body = await InvocarECapturarBodyAsync(
            new DomainException("Erro."));

        body.TryGetProperty("traceId", out JsonElement traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeveRetornar499QuandoClienteCancelouRequisicao()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        DefaultHttpContext context = new() { RequestAborted = cts.Token };
        using MemoryStream body = new();
        context.Response.Body = body;
        ErrorHandlingMiddleware middleware = new(
            _ => throw new OperationCanceledException(cts.Token),
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Length.Should().Be(0);
        context.Response.StatusCode.Should().Be(499);
    }

    [Fact]
    public async Task DeveTratarOperationCanceledExceptionSemRequestAbortedComoErro500()
    {
        DefaultHttpContext context = new();
        using MemoryStream body = new();
        context.Response.Body = body;
        ErrorHandlingMiddleware middleware = new(
            _ => throw new OperationCanceledException("Timeout interno, não cancelamento do cliente."),
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task DeveMapearDomainAuthExceptionParaStatus401()
    {
        HttpContext context = await InvocarMiddlewareComExcecaoAsync(
            new DomainAuthException("Credenciais inválidas."));

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task DeveCapturarDomainAuthExceptionAntesDeDomainException()
    {
        HttpContext context = await InvocarMiddlewareComExcecaoAsync(
            new DomainAuthException("Credenciais inválidas."));

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.StatusCode.Should().NotBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeveRetornarProblemDetailsComTipoErroDeAutenticacaoParaDomainAuthException()
    {
        JsonElement body = await InvocarECapturarBodyAsync(
            new DomainAuthException("Credenciais inválidas."));

        body.GetProperty("type").GetString().Should().Be("ErroDeAutenticacao");
        body.GetProperty("status").GetInt32().Should().Be(401);
        body.GetProperty("errors")[0].GetString().Should().Be("Credenciais inválidas.");
    }

    [Fact]
    public async Task DeveDeixarRespostaIntactaQuandoNaoHaExcecao()
    {
        DefaultHttpContext context = new();
        using MemoryStream body = new();
        context.Response.Body = body;
        ErrorHandlingMiddleware middleware = new(
            _ => Task.CompletedTask,
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Body.Length.Should().Be(0);
    }

    private static async Task<HttpContext> InvocarMiddlewareComExcecaoAsync(Exception excecao)
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        ErrorHandlingMiddleware middleware = new(
            _ => throw excecao,
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return context;
    }

    private static async Task<JsonElement> InvocarECapturarBodyAsync(Exception excecao)
    {
        HttpContext context = await InvocarMiddlewareComExcecaoAsync(excecao);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement;
    }
}
