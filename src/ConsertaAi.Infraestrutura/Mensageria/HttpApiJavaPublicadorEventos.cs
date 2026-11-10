using System.Net.Http.Json;
using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Aplicacao.Observabilidade;
using Microsoft.Extensions.Logging;

namespace ConsertaAi.Infraestrutura.Mensageria;

/// <summary>
/// Estratégia 2 (plano B): chama PATCH /internal/ocorrencias/{id}/apoios na API Java via HttpClient,
/// repassando o token do usuário.
/// </summary>
public sealed class HttpApiJavaPublicadorEventos : IPublicadorEventos
{
    public const string NomeCliente = "ApiJava";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpApiJavaPublicadorEventos> _logger;

    public HttpApiJavaPublicadorEventos(IHttpClientFactory httpClientFactory, ILogger<HttpApiJavaPublicadorEventos> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task PublicarAsync(ApoioAlteradoEvento evento, CancellationToken ct = default)
    {
        try
        {
            var cliente = _httpClientFactory.CreateClient(NomeCliente);
            var resposta = await cliente.PatchAsJsonAsync(
                $"internal/ocorrencias/{evento.OcorrenciaId}/apoios",
                new { totalApoios = evento.TotalApoios, tipo = evento.Tipo, eventoId = evento.EventoId },
                ct);

            if (!resposta.IsSuccessStatusCode)
            {
                AplicacaoMetricas.IntegracoesFalhasContador.Add(1, AplicacaoMetricas.Tag("integracao", "api_java"));
                _logger.LogWarning("API Java respondeu {Status} ao atualizar apoios da ocorrência {OcorrenciaId}",
                    (int)resposta.StatusCode, evento.OcorrenciaId);
            }
        }
        catch (Exception ex)
        {
            AplicacaoMetricas.IntegracoesFalhasContador.Add(1, AplicacaoMetricas.Tag("integracao", "api_java"));
            _logger.LogError(ex, "Falha ao notificar a API Java sobre {Tipo} da ocorrência {OcorrenciaId}", evento.Tipo, evento.OcorrenciaId);
        }
    }
}
