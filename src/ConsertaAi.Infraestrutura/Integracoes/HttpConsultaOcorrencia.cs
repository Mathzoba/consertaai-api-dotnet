using System.Net;
using System.Text.Json;
using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Aplicacao.Observabilidade;
using ConsertaAi.Infraestrutura.Mensageria;
using Microsoft.Extensions.Logging;

namespace ConsertaAi.Infraestrutura.Integracoes;

/// <summary>Consulta GET /ocorrencias/{id} na API Java para validar a ocorrência antes de apoiar/comentar.</summary>
public sealed class HttpConsultaOcorrencia : IConsultaOcorrencia
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpConsultaOcorrencia> _logger;

    public HttpConsultaOcorrencia(IHttpClientFactory httpClientFactory, ILogger<HttpConsultaOcorrencia> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ConsultaOcorrenciaResultado> ConsultarAsync(long ocorrenciaId, CancellationToken ct = default)
    {
        try
        {
            var cliente = _httpClientFactory.CreateClient(HttpApiJavaPublicadorEventos.NomeCliente);
            using var resposta = await cliente.GetAsync($"ocorrencias/{ocorrenciaId}", ct);

            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return ConsultaOcorrenciaResultado.NaoEncontrada;

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning("API Java respondeu {Status} ao consultar a ocorrência {OcorrenciaId}", (int)resposta.StatusCode, ocorrenciaId);
                return ConsultaOcorrenciaResultado.Indisponivel;
            }

            await using var conteudo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(conteudo, cancellationToken: ct);
            return ConsultaOcorrenciaResultado.Encontrada(LerStatus(json.RootElement));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Resiliência: se a Java estiver fora do ar, a participação não é bloqueada
            AplicacaoMetricas.IntegracoesFalhasContador.Add(1, AplicacaoMetricas.Tag("integracao", "api_java"));
            _logger.LogWarning(ex, "API Java indisponível ao consultar a ocorrência {OcorrenciaId}", ocorrenciaId);
            return ConsultaOcorrenciaResultado.Indisponivel;
        }
    }

    private static string? LerStatus(JsonElement raiz)
    {
        // Aceita tanto { "status": "ABERTA" } quanto um envelope { "data": { "status": ... } }
        foreach (var candidato in new[] { raiz, raiz.TryGetProperty("data", out var data) ? data : default })
        {
            if (candidato.ValueKind != JsonValueKind.Object) continue;
            foreach (var propriedade in candidato.EnumerateObject())
            {
                if (string.Equals(propriedade.Name, "status", StringComparison.OrdinalIgnoreCase) && propriedade.Value.ValueKind == JsonValueKind.String)
                    return propriedade.Value.GetString();
            }
        }
        return null;
    }
}
