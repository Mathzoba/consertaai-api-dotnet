using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Seguranca;

namespace ConsertaAi.Aplicacao.Servicos;

/// <summary>Casos de uso de apoio a ocorrências (participação do cidadão).</summary>
public interface IApoioServico
{
    Task<Resultado<ApoioResponse>> RegistrarAsync(RegistrarApoioRequest request, UsuarioAutenticado usuario, CancellationToken ct = default);
    Task<Resultado> RetirarAsync(long ocorrenciaId, UsuarioAutenticado usuario, CancellationToken ct = default);
    Task<ResumoApoiosResponse> ObterResumoAsync(long ocorrenciaId, UsuarioAutenticado usuario, CancellationToken ct = default);
}
