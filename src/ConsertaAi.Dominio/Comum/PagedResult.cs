namespace ConsertaAi.Dominio.Comum;

/// <summary>
/// Envelope genérico imutável contendo a lista de dados fatiada e os metadados da paginação
/// (mesmo contrato visto na aula de Paginação com MongoDB e evoluído na aula de HATEOAS).
/// </summary>
public record PagedResult<T>(
    IEnumerable<T> Itens,
    long TotalRegistros,
    int PaginaAtual,
    int TamanhoPagina)
{
    /// <summary>Total de páginas calculado a partir do total de registros e do tamanho da página.</summary>
    public int TotalPaginas => TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling((double)TotalRegistros / TamanhoPagina);

    /// <summary>Indica se existe página anterior (usado para gerar o link HATEOAS "previous").</summary>
    public bool HasPreviousPage => PaginaAtual > 1;

    /// <summary>Indica se existe próxima página (usado para gerar o link HATEOAS "next").</summary>
    public bool HasNextPage => PaginaAtual < TotalPaginas;

    /// <summary>Converte os itens mantendo os metadados de paginação.</summary>
    public PagedResult<TDestino> Mapear<TDestino>(Func<T, TDestino> conversor) =>
        new(Itens.Select(conversor).ToList(), TotalRegistros, PaginaAtual, TamanhoPagina);
}
