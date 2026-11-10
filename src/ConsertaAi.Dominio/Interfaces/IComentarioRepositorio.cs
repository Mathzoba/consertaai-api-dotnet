using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Entidades;

namespace ConsertaAi.Dominio.Interfaces;

/// <summary>Contrato de persistência dos comentários e de suas etiquetas (N:N).</summary>
public interface IComentarioRepositorio
{
    /// <summary>Busca o comentário carregando as etiquetas com Include/ThenInclude.</summary>
    Task<Comentario?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<Comentario>> ListarPorOcorrenciaAsync(long ocorrenciaId, int pagina, int tamanhoPagina, bool ascendente, CancellationToken ct = default);
    Task AdicionarAsync(Comentario comentario, CancellationToken ct = default);
    Task AtualizarAsync(Comentario comentario, CancellationToken ct = default);
    Task RemoverAsync(Comentario comentario, CancellationToken ct = default);
}
