using System.Text.Json;
using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Aplicacao.Observabilidade;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ConsertaAi.Infraestrutura.Mensageria;

/// <summary>Estratégia 1 (opção A do plano): publica o evento na exchange topic do RabbitMQ.</summary>
public sealed class RabbitMqPublicadorEventos : IPublicadorEventos
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqConexao _conexao;
    private readonly ILogger<RabbitMqPublicadorEventos> _logger;

    public RabbitMqPublicadorEventos(RabbitMqConexao conexao, ILogger<RabbitMqPublicadorEventos> logger)
    {
        _conexao = conexao;
        _logger = logger;
    }

    public Task PublicarAsync(ApoioAlteradoEvento evento, CancellationToken ct = default)
    {
        using var activity = AplicacaoMetricas.ActivitySourceAplicacao.StartActivity("PublicarEventoRabbitMq");
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", evento.Tipo);

        try
        {
            using var canal = _conexao.ObterConexao().CreateModel();

            var propriedades = canal.CreateBasicProperties();
            propriedades.Persistent = true;
            propriedades.ContentType = "application/json";
            propriedades.MessageId = evento.EventoId.ToString();
            propriedades.Type = evento.Tipo;
            propriedades.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var corpo = JsonSerializer.SerializeToUtf8Bytes(evento, Json);
            canal.BasicPublish(_conexao.Opcoes.Exchange, evento.Tipo, propriedades, corpo);

            _logger.LogInformation("Evento {Tipo} publicado no RabbitMQ para a ocorrência {OcorrenciaId} (total {TotalApoios})",
                evento.Tipo, evento.OcorrenciaId, evento.TotalApoios);
        }
        catch (Exception ex)
        {
            // O apoio já foi salvo: a falha de mensageria não pode desfazer a ação do cidadão
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            AplicacaoMetricas.IntegracoesFalhasContador.Add(1, AplicacaoMetricas.Tag("integracao", "rabbitmq"));
            _logger.LogError(ex, "Falha ao publicar o evento {Tipo} da ocorrência {OcorrenciaId} no RabbitMQ", evento.Tipo, evento.OcorrenciaId);
        }

        return Task.CompletedTask;
    }
}
