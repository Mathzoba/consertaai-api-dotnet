namespace ConsertaAi.Aplicacao.Integracoes;

/// <summary>Situação da consulta de uma ocorrência na API Java.</summary>
public enum SituacaoConsultaOcorrencia
{
    /// <summary>Validação desligada por configuração: a API confia no id recebido.</summary>
    NaoVerificada,
    Encontrada,
    NaoEncontrada,
    /// <summary>API Java fora do ar: a participação não é bloqueada (resiliência).</summary>
    Indisponivel
}

/// <summary>Resultado da consulta de uma ocorrência na API Java.</summary>
public sealed record ConsultaOcorrenciaResultado(SituacaoConsultaOcorrencia Situacao, string? Status = null)
{
    private static readonly string[] StatusQueAceitamApoio = ["ABERTA", "EM_ANALISE", "EM_ANDAMENTO"];

    public static ConsultaOcorrenciaResultado NaoVerificada { get; } = new(SituacaoConsultaOcorrencia.NaoVerificada);
    public static ConsultaOcorrenciaResultado NaoEncontrada { get; } = new(SituacaoConsultaOcorrencia.NaoEncontrada);
    public static ConsultaOcorrenciaResultado Indisponivel { get; } = new(SituacaoConsultaOcorrencia.Indisponivel);
    public static ConsultaOcorrenciaResultado Encontrada(string? status) => new(SituacaoConsultaOcorrencia.Encontrada, status);

    /// <summary>Só faz sentido apoiar ocorrências que ainda não foram resolvidas ou canceladas.</summary>
    public bool PermiteApoio =>
        Situacao != SituacaoConsultaOcorrencia.Encontrada
        || Status is null
        || StatusQueAceitamApoio.Contains(Status.ToUpperInvariant());

    /// <summary>Comentários são aceitos em qualquer ocorrência, exceto canceladas.</summary>
    public bool PermiteComentario =>
        Situacao != SituacaoConsultaOcorrencia.Encontrada
        || !string.Equals(Status, "CANCELADA", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Consulta à API Java (dona das ocorrências) para validar o id recebido.</summary>
public interface IConsultaOcorrencia
{
    Task<ConsultaOcorrenciaResultado> ConsultarAsync(long ocorrenciaId, CancellationToken ct = default);
}
