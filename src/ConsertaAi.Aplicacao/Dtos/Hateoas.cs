namespace ConsertaAi.Aplicacao.Dtos;

/// <summary>
/// DTO que representa um hiperlink RESTful (HATEOAS) individual.
/// </summary>
/// <param name="Href">URL completa para execução da ação.</param>
/// <param name="Rel">Relação da ação (ex.: "self", "update", "delete", "next").</param>
/// <param name="Method">Método HTTP a ser utilizado (GET, POST, PUT, DELETE).</param>
public record LinkDto(string Href, string Rel, string Method);

/// <summary>Wrapper genérico que une qualquer recurso de dados aos seus links HATEOAS.</summary>
public record ResourceEnvelope<T>(T Data, List<LinkDto> Links);

/// <summary>Resposta paginada com HATEOAS: cada item envelopado com seus links + links de navegação.</summary>
public record PaginaHateoas<T>(
    IEnumerable<ResourceEnvelope<T>> Itens,
    long TotalRegistros,
    int PaginaAtual,
    int TamanhoPagina,
    int TotalPaginas,
    List<LinkDto> Links);
