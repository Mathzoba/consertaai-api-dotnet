using System.Text.Json;
using ConsertaAi.Api.Respostas;
using ConsertaAi.Aplicacao.Observabilidade;
using ConsertaAi.Dominio.Excecoes;
using Microsoft.AspNetCore.Diagnostics;

namespace ConsertaAi.Api.Middlewares;

/// <summary>
/// Tratador global de exceções (IExceptionHandler do .NET 8): converte exceções em ProblemDetails
/// com o código HTTP adequado, sem expor stack trace ao cliente.
/// </summary>
public sealed class TratadorExcecoesGlobal : IExceptionHandler
{
    private readonly ILogger<TratadorExcecoesGlobal> _logger;

    public TratadorExcecoesGlobal(ILogger<TratadorExcecoesGlobal> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo, detalhe) = exception switch
        {
            ExcecaoDominio ex => (StatusCodes.Status400BadRequest, "Regra de domínio violada", ex.Message),
            OperacaoNaoPermitidaException ex => (StatusCodes.Status403Forbidden, "Acesso negado", ex.Message),
            UnauthorizedAccessException ex => (StatusCodes.Status401Unauthorized, "Não autenticado", ex.Message),
            BadHttpRequestException ex => (ex.StatusCode, "Requisição inválida", "O corpo ou os parâmetros da requisição estão em formato inválido."),
            JsonException => (StatusCodes.Status400BadRequest, "JSON inválido", "O corpo da requisição não é um JSON válido."),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado. Informe o correlationId ao suporte.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            AplicacaoMetricas.ErrosContador.Add(1, AplicacaoMetricas.Tag("tipo", "nao_tratado"));
            _logger.LogError(exception, "Erro não tratado em {Metodo} {Caminho}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning("Requisição rejeitada ({Status}): {Mensagem}", status, exception.Message);
        }

        if (httpContext.Response.HasStarted)
            return false;

        await Problemas.EscreverAsync(httpContext, status, titulo, detalhe);
        return true;
    }
}
