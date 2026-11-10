using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Interfaces;
using ConsertaAi.Infraestrutura.Mongo;
using MongoDB.Driver;

namespace ConsertaAi.Infraestrutura.Repositorios;

/// <summary>
/// Repositório NoSQL (MongoDB.Driver) da linha do tempo de participação.
/// Filtros tipo-seguros com Builders&lt;T&gt;.Filter, ordenação com Builders&lt;T&gt;.Sort e
/// paginação (Skip/Limit) executadas diretamente no MongoDB — padrão da aula de Paginação.
/// </summary>
public class MongoAtividadeRepositorio : IAtividadeRepositorio
{
    private readonly MongoContexto _contexto;

    public MongoAtividadeRepositorio(MongoContexto contexto) => _contexto = contexto;

    public async Task AdicionarAsync(Atividade atividade, CancellationToken ct = default)
    {
        await _contexto.GarantirIndicesAsync();
        await _contexto.Atividades.InsertOneAsync(atividade, cancellationToken: ct);
    }

    public async Task<PagedResult<Atividade>> ObterPaginadoAsync(
        long ocorrenciaId,
        TipoAtividade? tipo,
        bool ascendente,
        int pagina,
        int tamanhoPagina,
        CancellationToken ct = default)
    {
        // Instancia o construtor de filtros tipo-seguro do MongoDB
        var builder = Builders<Atividade>.Filter;

        // Filtro obrigatório: atividades da ocorrência informada
        var filterDef = builder.Eq(a => a.OcorrenciaId, ocorrenciaId);

        // Filtro opcional: tipo de atividade (ex.: somente ApoioRegistrado)
        if (tipo.HasValue)
            filterDef &= builder.Eq(a => a.Tipo, tipo.Value);

        // Conta o total de documentos filtrados sem carregar a lista em memória
        var totalRegistros = await _contexto.Atividades.CountDocumentsAsync(filterDef, cancellationToken: ct);

        // Ordenação pela data de registro (mais recentes primeiro por padrão)
        var sortDef = ascendente
            ? Builders<Atividade>.Sort.Ascending(a => a.DataRegistro)
            : Builders<Atividade>.Sort.Descending(a => a.DataRegistro);

        // Aplica filtro, ordenação e a fatia da página no servidor do MongoDB
        var itens = await _contexto.Atividades.Find(filterDef)
            .Sort(sortDef)
            .Skip((pagina - 1) * tamanhoPagina)
            .Limit(tamanhoPagina)
            .ToListAsync(ct);

        return new PagedResult<Atividade>(itens, totalRegistros, pagina, tamanhoPagina);
    }
}
