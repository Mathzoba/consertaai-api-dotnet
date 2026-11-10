using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;

namespace ConsertaAi.Aplicacao.Servicos;

/// <summary>Casos de uso do cadastro de etiquetas (mantido pelo gestor).</summary>
public interface IEtiquetaServico
{
    Task<IReadOnlyList<EtiquetaResponse>> ListarAsync(CancellationToken ct = default);
    Task<Resultado<EtiquetaResponse>> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Resultado<EtiquetaResponse>> CriarAsync(SalvarEtiquetaRequest request, CancellationToken ct = default);
    Task<Resultado<EtiquetaResponse>> AtualizarAsync(Guid id, SalvarEtiquetaRequest request, CancellationToken ct = default);
    Task<Resultado> RemoverAsync(Guid id, CancellationToken ct = default);
}
