using System.Diagnostics;
using ConsertaAi.Infraestrutura.Configuracoes;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ConsertaAi.Infraestrutura.Health;

/// <summary>
/// Check HTTP da API Java. Registrado com FailureStatus = Degraded: se a Java cair,
/// a API .NET continua aceitando apoios e comentários, mas o /health mostra "Degraded".
/// </summary>
public class ApiJavaHealthCheck : IHealthCheck
{
    public const string NomeCliente = "ApiJavaHealth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiJavaOptions _opcoes;

    public ApiJavaHealthCheck(IHttpClientFactory httpClientFactory, IOptions<ApiJavaOptions> opcoes)
    {
        _httpClientFactory = httpClientFactory;
        _opcoes = opcoes.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            var cliente = _httpClientFactory.CreateClient(NomeCliente);
            using var resposta = await cliente.GetAsync(_opcoes.HealthPath.TrimStart('/'), cancellationToken);

            var dados = new Dictionary<string, object>
            {
                ["Url"] = new Uri(cliente.BaseAddress!, _opcoes.HealthPath.TrimStart('/')).ToString(),
                ["StatusHttp"] = (int)resposta.StatusCode,
                ["LatenciaMs"] = cronometro.ElapsedMilliseconds
            };

            return resposta.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("API Java respondendo.", dados)
                : new HealthCheckResult(context.Registration.FailureStatus, $"API Java respondeu HTTP {(int)resposta.StatusCode}.", data: dados);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "API Java indisponível.", ex);
        }
    }
}
