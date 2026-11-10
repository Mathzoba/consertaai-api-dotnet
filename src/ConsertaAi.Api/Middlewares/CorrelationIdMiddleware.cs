using System.Diagnostics;
using Serilog.Context;

namespace ConsertaAi.Api.Middlewares;

/// <summary>
/// Middleware responsável pela gestão e propagação do Correlation ID (padrão da Aula 02).
/// Lê o header X-Correlation-ID (ou gera um GUID), devolve na resposta e injeta a propriedade
/// "CorrelationId" no LogContext do Serilog durante todo o ciclo de vida da requisição.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    private const string ChaveItem = "CorrelationId";
    private const int TamanhoMaximo = 100;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Obtém o Correlation ID do cabeçalho da requisição ou gera um novo GUID
        var recebido = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(recebido) || recebido.Length > TamanhoMaximo
            ? Guid.NewGuid().ToString()
            : recebido;

        context.Items[ChaveItem] = correlationId;

        // Devolve o Correlation ID nos cabeçalhos da resposta HTTP
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Relaciona logs (Serilog) e traces (OpenTelemetry) pela mesma chave
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    public static string Obter(HttpContext context) =>
        context.Items.TryGetValue(ChaveItem, out var valor) && valor is string id ? id : context.TraceIdentifier;
}
