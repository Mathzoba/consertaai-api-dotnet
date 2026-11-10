using ConsertaAi.Api.Filtros;
using ConsertaAi.Api.Hateoas;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Servicos;

namespace ConsertaAi.Api.Endpoints;

/// <summary>Linha do tempo de participação (MongoDB) com filtro, ordenação e paginação no banco.</summary>
public static class AtividadeEndpoints
{
    public static IEndpointRouteBuilder MapAtividadeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ocorrencias/{ocorrenciaId:long}/atividades", ListarAsync)
            .WithName("ListarAtividades")
            .WithTags("Linha do tempo (MongoDB)")
            .WithSummary("Linha do tempo de apoios e comentários de uma ocorrência")
            .WithDescription("Consulta a collection 'atividades' do MongoDB com Builders<T>.Filter (tipo opcional), Builders<T>.Sort (data) e Skip/Limit.")
            .RequireAuthorization()
            .Validar<AtividadeFiltroRequest>()
            .Produces<PaginaHateoas<AtividadeResponse>>()
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> ListarAsync(
        long ocorrenciaId, [AsParameters] AtividadeFiltroRequest filtro, IAtividadeServico servico, IGeradorLinks links, CancellationToken ct)
    {
        var pagina = await servico.ListarAsync(ocorrenciaId, filtro, ct);
        return Results.Ok(pagina.ComLinks(_ => [], links.ParaPaginaAtividades(ocorrenciaId, pagina, filtro)));
    }
}
