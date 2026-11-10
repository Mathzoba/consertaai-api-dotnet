using ConsertaAi.Api.Configuracao;
using ConsertaAi.Api.Filtros;
using ConsertaAi.Api.Hateoas;
using ConsertaAi.Api.Respostas;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Servicos;

namespace ConsertaAi.Api.Endpoints;

/// <summary>CRUD de etiquetas: leitura para qualquer usuário autenticado, escrita somente para GESTOR.</summary>
public static class EtiquetaEndpoints
{
    public static IEndpointRouteBuilder MapEtiquetaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/etiquetas").WithTags("Etiquetas").RequireAuthorization().WithOpenApi();

        grupo.MapGet("", ListarAsync)
            .WithName("ListarEtiquetas")
            .WithSummary("Lista as etiquetas disponíveis para classificar comentários")
            .Produces<ResourceEnvelope<IEnumerable<ResourceEnvelope<EtiquetaResponse>>>>();

        grupo.MapGet("/{id:guid}", ObterAsync)
            .WithName("ObterEtiqueta")
            .WithSummary("Detalhe de uma etiqueta")
            .Produces<ResourceEnvelope<EtiquetaResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("", CriarAsync)
            .WithName("CriarEtiqueta")
            .WithSummary("Cria uma etiqueta (GESTOR)")
            .RequireAuthorization(Politicas.GestorOnly)
            .Validar<SalvarEtiquetaRequest>()
            .Produces<ResourceEnvelope<EtiquetaResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPut("/{id:guid}", AtualizarAsync)
            .WithName("AtualizarEtiqueta")
            .WithSummary("Atualiza uma etiqueta (GESTOR)")
            .RequireAuthorization(Politicas.GestorOnly)
            .Validar<SalvarEtiquetaRequest>()
            .Produces<ResourceEnvelope<EtiquetaResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapDelete("/{id:guid}", RemoverAsync)
            .WithName("RemoverEtiqueta")
            .WithSummary("Remove uma etiqueta (GESTOR)")
            .RequireAuthorization(Politicas.GestorOnly)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListarAsync(IEtiquetaServico servico, IGeradorLinks links, CancellationToken ct)
    {
        var etiquetas = await servico.ListarAsync(ct);
        var itens = etiquetas.Select(e => new ResourceEnvelope<EtiquetaResponse>(e, links.ParaEtiqueta(e)));
        return Results.Ok(new ResourceEnvelope<IEnumerable<ResourceEnvelope<EtiquetaResponse>>>(itens, links.ParaColecaoEtiquetas()));
    }

    private static async Task<IResult> ObterAsync(Guid id, IEtiquetaServico servico, IGeradorLinks links, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.ObterPorIdAsync(id, ct);
        return resultado.ParaHttp(http, e => Results.Ok(new ResourceEnvelope<EtiquetaResponse>(e, links.ParaEtiqueta(e))));
    }

    private static async Task<IResult> CriarAsync(SalvarEtiquetaRequest request, IEtiquetaServico servico, IGeradorLinks links, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(request, ct);
        return resultado.ParaHttp(http, e => Results.Created($"/etiquetas/{e.Id}", new ResourceEnvelope<EtiquetaResponse>(e, links.ParaEtiqueta(e))));
    }

    private static async Task<IResult> AtualizarAsync(Guid id, SalvarEtiquetaRequest request, IEtiquetaServico servico, IGeradorLinks links, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.AtualizarAsync(id, request, ct);
        return resultado.ParaHttp(http, e => Results.Ok(new ResourceEnvelope<EtiquetaResponse>(e, links.ParaEtiqueta(e))));
    }

    private static async Task<IResult> RemoverAsync(Guid id, IEtiquetaServico servico, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.RemoverAsync(id, ct);
        return resultado.ParaNoContent(http);
    }
}
