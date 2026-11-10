using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;

namespace ConsertaAi.Dominio.Interfaces;

/// <summary>Contrato do repositório NoSQL (MongoDB) da linha do tempo de participação.</summary>
public interface IAtividadeRepositorio
{
    Task AdicionarAsync(Atividade atividade, CancellationToken ct = default);

    Task<PagedResult<Atividade>> ObterPaginadoAsync(
        long ocorrenciaId,
        TipoAtividade? tipo,
        bool ascendente,
        int pagina,
        int tamanhoPagina,
        CancellationToken ct = default);
}
