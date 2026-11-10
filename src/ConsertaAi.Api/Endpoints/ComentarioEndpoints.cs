using System.Security.Claims;
using ConsertaAi.Api.Configuracao;
using ConsertaAi.Api.Filtros;
using ConsertaAi.Api.Hateoas;
using ConsertaAi.Api.Middlewares;
using ConsertaAi.Api.Respostas;
using ConsertaAi.Api.Seguranca;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Servicos;

namespace ConsertaAi.Api.Endpoints;

/// <summary>CRUD de comentários (tabela COMENTARIO + etiquetas N:N).</summary>
public static class ComentarioEndpoints
{
    private const string Tag = "Comentários";

    public static IEndpointRouteBuilder MapComentarioEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ocorrencias/{ocorrenciaId:long}/comentarios", ListarAsync)
            .WithName("ListarComentarios")
            .WithTags(Tag)
            .WithSummary("Lista paginada dos comentários de uma ocorrência")
            .WithDescription("Paginação executada no banco. Cada item traz links HATEOAS: 'update' só para o autor; 'delete' para o autor ou gestor.")
            .RequireAuthorization()
            .Validar<PaginacaoRequest>()
            .Produces<PaginaHateoas<ComentarioResponse>>()
            .WithOpenApi();

        var grupo = app.MapGroup("/comentarios").WithTags(Tag).RequireAuthorization().WithOpenApi();

        grupo.MapGet("/{id:guid}", ObterAsync)
            .WithName("ObterComentario")
            .WithSummary("Detalhe de um comentário com suas etiquetas")
            .Produces<ResourceEnvelope<ComentarioResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("", CriarAsync)
            .WithName("CriarComentario")
            .WithSummary("Cria um comentário em uma ocorrência")
            .WithDescription(
                "Texto moderado por estratégias (tamanho, linguagem ofensiva e anti-spam). Etiquetas opcionais (N:N). " +
                "Exige Idempotency-Key e respeita o limite de 5 POSTs por minuto por usuário.")
            .RequireRateLimiting(RateLimitConfiguracao.PoliticaEscrita)
            .RequerIdempotencia()
            .Validar<CriarComentarioRequest>()
            .Produces<ResourceEnvelope<ComentarioResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        grupo.MapPut("/{id:guid}", AtualizarAsync)
            .WithName("AtualizarComentario")
            .WithSummary("Edita o comentário (somente o autor)")
            .Validar<AtualizarComentarioRequest>()
            .Produces<ResourceEnvelope<ComentarioResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", RemoverAsync)
            .WithName("RemoverComentario")
            .WithSummary("Remove o comentário (autor ou GESTOR)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        long ocorrenciaId, [AsParameters] PaginacaoRequest paginacao, IComentarioServico servico, IGeradorLinks links, CancellationToken ct)
    {
        var pagina = await servico.ListarPorOcorrenciaAsync(ocorrenciaId, paginacao, ct);
        return Results.Ok(pagina.ComLinks(links.ParaComentario, links.ParaPaginaComentarios(ocorrenciaId, pagina, paginacao)));
    }

    private static async Task<IResult> ObterAsync(Guid id, IComentarioServico servico, IGeradorLinks links, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.ObterPorIdAsync(id, ct);
        return resultado.ParaHttp(http, c => Results.Ok(new ResourceEnvelope<ComentarioResponse>(c, links.ParaComentario(c))));
    }

    private static async Task<IResult> CriarAsync(
        CriarComentarioRequest request, ClaimsPrincipal usuario, IComentarioServico servico, IGeradorLinks links, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(request, usuario.ObterUsuario(), ct);
        return resultado.ParaHttp(http, c => Results.Created($"/comentarios/{c.Id}", new ResourceEnvelope<ComentarioResponse>(c, links.ParaComentario(c))));
    }

    private static async Task<IResult> AtualizarAsync(
        Guid id, AtualizarComentarioRequest request, ClaimsPrincipal usuario, IComentarioServico servico, IGeradorLinks links, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.AtualizarAsync(id, request, usuario.ObterUsuario(), ct);
        return resultado.ParaHttp(http, c => Results.Ok(new ResourceEnvelope<ComentarioResponse>(c, links.ParaComentario(c))));
    }

    private static async Task<IResult> RemoverAsync(Guid id, ClaimsPrincipal usuario, IComentarioServico servico, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.RemoverAsync(id, usuario.ObterUsuario(), ct);
        return resultado.ParaNoContent(http);
    }
}
