using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ConsertaAi.Api.Configuracao;

/// <summary>
/// Os endpoints de MapHealthChecks não entram no ApiExplorer, então não apareceriam no Swagger.
/// Este filtro adiciona /health, /health/ready e /health/live ao documento OpenAPI para que
/// possam ser executados pelo próprio Swagger UI.
/// </summary>
public sealed class HealthChecksDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        Adicionar(swaggerDoc, "/health", "Saúde da API e de todas as dependências",
            "Verifica banco relacional (Oracle/Azure SQL), MongoDB, RabbitMQ e API Java. " +
            "Healthy/Degraded → 200; Unhealthy (banco fora do ar) → 503.");
        Adicionar(swaggerDoc, "/health/ready", "Prontidão (readiness)",
            "Somente as dependências obrigatórias (banco relacional). Usado pelo orquestrador para enviar tráfego.");
        Adicionar(swaggerDoc, "/health/live", "Vivacidade (liveness)",
            "Confirma que o processo da API está no ar, sem consultar dependências.");
    }

    private static void Adicionar(OpenApiDocument documento, string caminho, string resumo, string descricao)
    {
        var operacao = new OpenApiOperation
        {
            Tags = [new OpenApiTag { Name = "Health" }],
            Summary = resumo,
            Description = descricao,
            OperationId = "Health" + caminho.Replace("/health", string.Empty).Replace("/", string.Empty),
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "Healthy ou Degraded" },
                ["503"] = new OpenApiResponse { Description = "Unhealthy" }
            }
        };

        documento.Paths[caminho] = new OpenApiPathItem
        {
            Operations = new Dictionary<OperationType, OpenApiOperation> { [OperationType.Get] = operacao }
        };
    }
}
