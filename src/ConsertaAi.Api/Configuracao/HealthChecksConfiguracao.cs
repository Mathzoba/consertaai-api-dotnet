using System.Text.Json;
using ConsertaAi.Infraestrutura;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConsertaAi.Api.Configuracao;

/// <summary>
/// Endpoints de diagnóstico para orquestradores (Azure Container Instances, Kubernetes):
/// /health (todas as dependências), /health/ready (pronto para tráfego) e /health/live (processo vivo).
/// Healthy e Degraded → HTTP 200; Unhealthy → HTTP 503.
/// </summary>
public static class HealthChecksConfiguracao
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static WebApplication MapHealthChecksConsertaAi(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = EscreverRespostaAsync })
            .WithTags("Health").AllowAnonymous().DisableRateLimiting();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registro => registro.Tags.Contains(DependencyInjection.TagPronto),
            ResponseWriter = EscreverRespostaAsync
        }).AllowAnonymous().DisableRateLimiting();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = EscreverRespostaAsync
        }).AllowAnonymous().DisableRateLimiting();

        return app;
    }

    private static Task EscreverRespostaAsync(HttpContext contexto, HealthReport relatorio)
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var resposta = new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = Math.Round(relatorio.TotalDuration.TotalMilliseconds, 1),
            verificacoes = relatorio.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description,
                duracaoMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                dados = e.Value.Data.Count > 0 ? e.Value.Data : null,
                erro = e.Value.Exception?.Message
            })
        };

        return contexto.Response.WriteAsync(JsonSerializer.Serialize(resposta, Json));
    }
}
