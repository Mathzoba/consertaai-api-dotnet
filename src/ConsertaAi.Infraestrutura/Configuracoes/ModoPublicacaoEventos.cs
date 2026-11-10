namespace ConsertaAi.Infraestrutura.Configuracoes;

/// <summary>Como os eventos de apoio chegam à API Java (Strategy de publicação).</summary>
public enum ModoPublicacaoEventos
{
    /// <summary>Somente registra o evento no log (desenvolvimento sem RabbitMQ).</summary>
    Log,
    /// <summary>Publica na exchange do RabbitMQ (opção A do plano).</summary>
    RabbitMq,
    /// <summary>Plano B: PATCH /internal/ocorrencias/{id}/apoios na API Java via HttpClient.</summary>
    HttpApiJava
}
