using ConsertaAi.Aplicacao.Integracoes;

namespace ConsertaAi.Infraestrutura.Integracoes;

/// <summary>Usada quando ApiJava:ValidarOcorrencias=false: confia no id recebido (sem chamada externa).</summary>
public sealed class ConsultaOcorrenciaDesabilitada : IConsultaOcorrencia
{
    public Task<ConsultaOcorrenciaResultado> ConsultarAsync(long ocorrenciaId, CancellationToken ct = default) =>
        Task.FromResult(ConsultaOcorrenciaResultado.NaoVerificada);
}
