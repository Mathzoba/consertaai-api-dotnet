using ConsertaAi.Api.Seguranca;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Seguranca;
using ConsertaAi.Dominio.Comum;

namespace ConsertaAi.Api.Hateoas;

/// <summary>
/// Gera os links HATEOAS (REST nível 3). A URL base vem do HttpContext atual (IHttpContextAccessor)
/// e os links de escrita só aparecem quando o usuário autenticado pode executá-los —
/// a API "dita" quais botões o app deve exibir (ex.: "editar" só para o autor).
/// </summary>
public interface IGeradorLinks
{
    List<LinkDto> ParaApoio(ApoioResponse apoio);
    List<LinkDto> ParaResumoApoios(ResumoApoiosResponse resumo);
    List<LinkDto> ParaComentario(ComentarioResponse comentario);
    List<LinkDto> ParaPaginaComentarios(long ocorrenciaId, PagedResult<ComentarioResponse> pagina, PaginacaoRequest filtro);
    List<LinkDto> ParaEtiqueta(EtiquetaResponse etiqueta);
    List<LinkDto> ParaColecaoEtiquetas();
    List<LinkDto> ParaPaginaAtividades(long ocorrenciaId, PagedResult<AtividadeResponse> pagina, AtividadeFiltroRequest filtro);
}

public sealed class GeradorLinks : IGeradorLinks
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GeradorLinks(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    private HttpContext Contexto => _httpContextAccessor.HttpContext
                                    ?? throw new InvalidOperationException("HttpContext indisponível para gerar links HATEOAS.");

    private string BaseUrl => $"{Contexto.Request.Scheme}://{Contexto.Request.Host}{Contexto.Request.PathBase}";

    private bool EhGestor => Contexto.User.IsInRole(Papeis.Gestor);
    private bool EhCidadao => Contexto.User.IsInRole(Papeis.Cidadao);
    private string? UsuarioId => Contexto.User.ObterId();

    public List<LinkDto> ParaApoio(ApoioResponse apoio) =>
    [
        new($"{BaseUrl}/ocorrencias/{apoio.OcorrenciaId}/apoios", "self", "GET"),
        new($"{BaseUrl}/apoios/{apoio.OcorrenciaId}", "retirar-apoio", "DELETE"),
        new($"{BaseUrl}/ocorrencias/{apoio.OcorrenciaId}/comentarios", "comentarios", "GET"),
        new($"{BaseUrl}/ocorrencias/{apoio.OcorrenciaId}/atividades", "atividades", "GET")
    ];

    public List<LinkDto> ParaResumoApoios(ResumoApoiosResponse resumo)
    {
        var links = new List<LinkDto>
        {
            new($"{BaseUrl}/ocorrencias/{resumo.OcorrenciaId}/apoios", "self", "GET"),
            new($"{BaseUrl}/ocorrencias/{resumo.OcorrenciaId}/comentarios", "comentarios", "GET")
        };

        // Somente cidadãos apoiam: o app mostra "Apoiar" ou "Apoiado (retirar)" conforme o link recebido
        if (EhCidadao)
        {
            links.Add(resumo.UsuarioApoiou
                ? new LinkDto($"{BaseUrl}/apoios/{resumo.OcorrenciaId}", "retirar-apoio", "DELETE")
                : new LinkDto($"{BaseUrl}/apoios", "apoiar", "POST"));
        }

        return links;
    }

    public List<LinkDto> ParaComentario(ComentarioResponse comentario)
    {
        var url = $"{BaseUrl}/comentarios/{comentario.Id}";
        var links = new List<LinkDto>
        {
            new(url, "self", "GET"),
            new($"{BaseUrl}/ocorrencias/{comentario.OcorrenciaId}/comentarios", "comentarios-da-ocorrencia", "GET")
        };

        var ehAutor = UsuarioId == comentario.UsuarioId;

        // Somente o autor recebe o link de edição
        if (ehAutor)
            links.Add(new LinkDto(url, "update", "PUT"));

        // Autor ou gestor (moderação) recebem o link de exclusão
        if (ehAutor || EhGestor)
            links.Add(new LinkDto(url, "delete", "DELETE"));

        return links;
    }

    public List<LinkDto> ParaPaginaComentarios(long ocorrenciaId, PagedResult<ComentarioResponse> pagina, PaginacaoRequest filtro)
    {
        var links = GerarLinksPaginacao($"{BaseUrl}/ocorrencias/{ocorrenciaId}/comentarios", pagina, $"&ascendente={filtro.Ascendente.ToString().ToLowerInvariant()}");
        links.Add(new LinkDto($"{BaseUrl}/comentarios", "create-comentario", "POST"));
        return links;
    }

    public List<LinkDto> ParaEtiqueta(EtiquetaResponse etiqueta)
    {
        var url = $"{BaseUrl}/etiquetas/{etiqueta.Id}";
        var links = new List<LinkDto>
        {
            new(url, "self", "GET"),
            new($"{BaseUrl}/etiquetas", "all-etiquetas", "GET")
        };

        // Somente o gestor mantém o cadastro de etiquetas
        if (EhGestor)
        {
            links.Add(new LinkDto(url, "update", "PUT"));
            links.Add(new LinkDto(url, "delete", "DELETE"));
        }

        return links;
    }

    public List<LinkDto> ParaColecaoEtiquetas()
    {
        var links = new List<LinkDto> { new($"{BaseUrl}/etiquetas", "self", "GET") };
        if (EhGestor)
            links.Add(new LinkDto($"{BaseUrl}/etiquetas", "create-etiqueta", "POST"));
        return links;
    }

    public List<LinkDto> ParaPaginaAtividades(long ocorrenciaId, PagedResult<AtividadeResponse> pagina, AtividadeFiltroRequest filtro)
    {
        var extra = $"&ascendente={filtro.Ascendente.ToString().ToLowerInvariant()}" + (filtro.Tipo.HasValue ? $"&tipo={filtro.Tipo}" : string.Empty);
        return GerarLinksPaginacao($"{BaseUrl}/ocorrencias/{ocorrenciaId}/atividades", pagina, extra);
    }

    /// <summary>Links de navegação da paginação: self, first, previous, next e last (padrão da aula de HATEOAS).</summary>
    private static List<LinkDto> GerarLinksPaginacao<T>(string baseUrl, PagedResult<T> resultado, string queryExtra)
    {
        string Url(int pagina) => $"{baseUrl}?pagina={pagina}&tamanhoPagina={resultado.TamanhoPagina}{queryExtra}";

        var links = new List<LinkDto>
        {
            new(Url(resultado.PaginaAtual), "self", "GET"),
            new(Url(1), "first", "GET")
        };

        if (resultado.HasPreviousPage)
            links.Add(new LinkDto(Url(resultado.PaginaAtual - 1), "previous", "GET"));

        if (resultado.HasNextPage)
            links.Add(new LinkDto(Url(resultado.PaginaAtual + 1), "next", "GET"));

        if (resultado.TotalPaginas > 0)
            links.Add(new LinkDto(Url(resultado.TotalPaginas), "last", "GET"));

        return links;
    }
}

/// <summary>Monta as respostas paginadas com HATEOAS a partir do PagedResult.</summary>
public static class PaginaHateoasExtensions
{
    public static PaginaHateoas<T> ComLinks<T>(this PagedResult<T> pagina, Func<T, List<LinkDto>> linksItem, List<LinkDto> linksPagina) =>
        new(
            pagina.Itens.Select(item => new ResourceEnvelope<T>(item, linksItem(item))).ToList(),
            pagina.TotalRegistros,
            pagina.PaginaAtual,
            pagina.TamanhoPagina,
            pagina.TotalPaginas,
            linksPagina);
}
