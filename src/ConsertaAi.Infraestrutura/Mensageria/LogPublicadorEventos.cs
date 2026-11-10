using ConsertaAi.Aplicacao.Integracoes;
using Microsoft.Extensions.Logging;

namespace ConsertaAi.Infraestrutura.Mensageria;

/// <summary>Estratégia 3 (desenvolvimento): apenas registra o evento no log estruturado.</summary>
public sealed class LogPublicadorEventos : IPublicadorEventos
{
    private readonly ILogger<LogPublicadorEventos> _logger;

    public LogPublicadorEventos(ILogger<LogPublicadorEventos> logger) => _logger = logger;

    public Task PublicarAsync(ApoioAlteradoEvento evento, CancellationToken ct = default)
    {
        _logger.LogInformation("[Eventos:Modo=Log] Evento {Tipo} da ocorrência {OcorrenciaId} — total de apoios {TotalApoios}",
            evento.Tipo, evento.OcorrenciaId, evento.TotalApoios);
        return Task.CompletedTask;
    }
}
