namespace ConsertaAi.Aplicacao.Integracoes;

/// <summary>
/// Evento de integração publicado quando o total de apoios de uma ocorrência muda.
/// A API Java consome o evento para atualizar QTD_APOIOS e recalcular a prioridade.
/// </summary>
/// <param name="Tipo">Routing key: "apoio.registrado" ou "apoio.removido".</param>
/// <param name="OcorrenciaId">Id da ocorrência na API Java.</param>
/// <param name="UsuarioId">Id do usuário (claim sub).</param>
/// <param name="TotalApoios">Total de apoios após a operação (permite atualização idempotente no consumidor).</param>
/// <param name="DataEvento">Data/hora UTC do evento.</param>
public sealed record ApoioAlteradoEvento(string Tipo, long OcorrenciaId, string UsuarioId, int TotalApoios, DateTime DataEvento)
{
    public const string RoutingKeyRegistrado = "apoio.registrado";
    public const string RoutingKeyRemovido = "apoio.removido";

    public Guid EventoId { get; init; } = Guid.NewGuid();

    public static ApoioAlteradoEvento Registrado(long ocorrenciaId, string usuarioId, int total) =>
        new(RoutingKeyRegistrado, ocorrenciaId, usuarioId, total, DateTime.UtcNow);

    public static ApoioAlteradoEvento Removido(long ocorrenciaId, string usuarioId, int total) =>
        new(RoutingKeyRemovido, ocorrenciaId, usuarioId, total, DateTime.UtcNow);
}

/// <summary>
/// Strategy de publicação de eventos (DIP + OCP). Implementações na Infraestrutura:
/// RabbitMQ (padrão do plano), HTTP direto na API Java (plano B) ou apenas log (desenvolvimento).
/// Falhas de publicação não devem desfazer a operação do cidadão.
/// </summary>
public interface IPublicadorEventos
{
    Task PublicarAsync(ApoioAlteradoEvento evento, CancellationToken ct = default);
}
