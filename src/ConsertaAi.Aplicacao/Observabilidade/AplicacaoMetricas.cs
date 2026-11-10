using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConsertaAi.Aplicacao.Observabilidade;

/// <summary>
/// Classe centralizadora de Métricas Customizadas (Meter/Counter) e Traces (ActivitySource)
/// da aplicação, exportados pelo OpenTelemetry (mesmo padrão da Aula 03).
/// </summary>
public static class AplicacaoMetricas
{
    /// <summary>Nome do serviço usado para identificação no OpenTelemetry.</summary>
    public const string NomeServico = "ConsertaAi.Api.DotNet";

    /// <summary>Meter nativo do .NET para registro de métricas customizadas.</summary>
    public static readonly Meter MeterAplicacao = new(NomeServico, "1.0.0");

    /// <summary>ActivitySource para criação de Spans manuais de Tracing Distribuído.</summary>
    public static readonly ActivitySource ActivitySourceAplicacao = new(NomeServico, "1.0.0");

    public static readonly Counter<long> ApoiosRegistradosContador = MeterAplicacao.CreateCounter<long>(
        name: "apoios_registrados_total",
        unit: "{apoios}",
        description: "Total de apoios registrados (tag status: sucesso | duplicado | rejeitado)");

    public static readonly Counter<long> ApoiosRetiradosContador = MeterAplicacao.CreateCounter<long>(
        name: "apoios_retirados_total",
        unit: "{apoios}",
        description: "Total de apoios retirados pelos cidadãos");

    public static readonly Counter<long> ComentariosContador = MeterAplicacao.CreateCounter<long>(
        name: "comentarios_operacoes_total",
        unit: "{comentarios}",
        description: "Operações em comentários (tags operacao e status)");

    public static readonly Counter<long> IdempotenciaRepeticoesContador = MeterAplicacao.CreateCounter<long>(
        name: "idempotencia_requisicoes_repetidas_total",
        unit: "{requisicoes}",
        description: "Requisições repetidas atendidas pela resposta armazenada (sem gravar de novo)");

    public static readonly Counter<long> RateLimitRejeicoesContador = MeterAplicacao.CreateCounter<long>(
        name: "rate_limit_rejeicoes_total",
        unit: "{requisicoes}",
        description: "Requisições rejeitadas com HTTP 429 pelo Rate Limiter");

    public static readonly Counter<long> ErrosContador = MeterAplicacao.CreateCounter<long>(
        name: "erros_total",
        unit: "{erros}",
        description: "Erros de negócio e falhas não tratadas (tag tipo)");

    public static readonly Counter<long> IntegracoesFalhasContador = MeterAplicacao.CreateCounter<long>(
        name: "integracoes_falhas_total",
        unit: "{falhas}",
        description: "Falhas em integrações externas (RabbitMQ, MongoDB, API Java) que não interrompem o fluxo");

    public static KeyValuePair<string, object?> Tag(string chave, object? valor) => new(chave, valor);
}
