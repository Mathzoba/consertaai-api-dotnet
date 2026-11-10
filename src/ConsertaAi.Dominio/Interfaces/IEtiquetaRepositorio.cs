using ConsertaAi.Dominio.Entidades;

namespace ConsertaAi.Dominio.Interfaces;

/// <summary>Contrato de persistência das etiquetas usadas para classificar comentários.</summary>
public interface IEtiquetaRepositorio
{
    Task<IReadOnlyList<Etiqueta>> ListarAsync(CancellationToken ct = default);
    Task<Etiqueta?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Etiqueta>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<bool> ExisteComNomeAsync(string nome, Guid? ignorarId = null, CancellationToken ct = default);
    Task AdicionarAsync(Etiqueta etiqueta, CancellationToken ct = default);
    Task AtualizarAsync(Etiqueta etiqueta, CancellationToken ct = default);
    Task RemoverAsync(Etiqueta etiqueta, CancellationToken ct = default);
}
