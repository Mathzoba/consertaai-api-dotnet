using System.Diagnostics;
using ConsertaAi.Api.Middlewares;
using ConsertaAi.Aplicacao.Comum;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaAi.Api.Respostas;

/// <summary>
/// Padroniza todas as respostas de erro no formato ProblemDetails (RFC 7807),
/// sempre com correlationId e traceId para facilitar o rastreio nos logs.
/// </summary>
public static class Problemas
{
    public static ProblemDetails Criar(HttpContext contexto, int status, string titulo, string? detalhe)
    {
        var problema = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = detalhe,
            Instance = contexto.Request.Path,
            Type = $"https://httpstatuses.io/{status}"
        };
        Enriquecer(contexto, problema);
        return problema;
    }

    public static void Enriquecer(HttpContext contexto, ProblemDetails problema)
    {
        problema.Extensions["correlationId"] = CorrelationIdMiddleware.Obter(contexto);
        problema.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier;
    }

    public static Task EscreverAsync(HttpContext contexto, int status, string titulo, string? detalhe)
    {
        contexto.Response.StatusCode = status;
        return contexto.Response.WriteAsJsonAsync(Criar(contexto, status, titulo, detalhe),
            options: null, contentType: "application/problem+json");
    }

    /// <summary>Converte um erro da camada de Aplicação no código HTTP adequado.</summary>
    public static IResult ParaHttp(this Erro erro, HttpContext contexto) => erro.Tipo switch
    {
        TipoErro.Validacao => Validacao(contexto, erro),
        TipoErro.NaoEncontrado => Resultado(contexto, StatusCodes.Status404NotFound, "Recurso não encontrado", erro.Mensagem),
        TipoErro.Conflito => Resultado(contexto, StatusCodes.Status409Conflict, "Conflito", erro.Mensagem),
        TipoErro.Proibido => Resultado(contexto, StatusCodes.Status403Forbidden, "Acesso negado", erro.Mensagem),
        TipoErro.RegraNegocio => Resultado(contexto, StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", erro.Mensagem),
        _ => Resultado(contexto, StatusCodes.Status500InternalServerError, "Erro inesperado", erro.Mensagem)
    };

    public static IResult Resultado(HttpContext contexto, int status, string titulo, string? detalhe)
    {
        var problema = Criar(contexto, status, titulo, detalhe);
        return Results.Json(problema, statusCode: status, contentType: "application/problem+json");
    }

    public static IResult Validacao(HttpContext contexto, IDictionary<string, string[]> erros, string titulo = "Um ou mais campos são inválidos.")
    {
        var problema = new HttpValidationProblemDetails(erros)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = titulo,
            Instance = contexto.Request.Path,
            Type = "https://httpstatuses.io/400"
        };
        Enriquecer(contexto, problema);
        return Results.Json(problema, statusCode: StatusCodes.Status400BadRequest, contentType: "application/problem+json");
    }

    private static IResult Validacao(HttpContext contexto, Erro erro) =>
        Validacao(contexto, erro.Campos?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string[]>(), erro.Mensagem);
}
