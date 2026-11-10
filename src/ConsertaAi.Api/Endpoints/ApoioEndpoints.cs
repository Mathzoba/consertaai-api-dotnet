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

/// <summary>Endpoints de apoio a ocorrências (funcionalidade principal de valor ao cidadão).</summary>
public static class ApoioEndpoints
{
    private const string Tag = "Apoios";

    public static IEndpointRouteBuilder MapApoioEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/apoios", RegistrarAsync)
            .WithName("RegistrarApoio")
            .WithTags(Tag)
            .WithSummary("Apoia uma ocorrência já reportada (CIDADAO)")
            .WithDescription(
                "Em vez de criar um report duplicado, o cidadão apoia a ocorrência existente, aumentando a prioridade dela na fila do gestor. " +
                "Exige o header Idempotency-Key: repetir a mesma requisição devolve a mesma resposta sem gravar de novo. " +
                "Limite de 5 POSTs por minuto por usuário (HTTP 429). Publica o evento 'apoio.registrado' para a API Java.")
            .RequireAuthorization(Politicas.CidadaoOnly)
            .RequireRateLimiting(RateLimitConfiguracao.PoliticaEscrita)
            .RequerIdempotencia()
            .Validar<RegistrarApoioRequest>()
            .Produces<ResourceEnvelope<ApoioResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithOpenApi();

        app.MapDelete("/apoios/{ocorrenciaId:long}", RetirarAsync)
            .WithName("RetirarApoio")
            .WithTags(Tag)
            .WithSummary("Retira o apoio do usuário autenticado (CIDADAO)")
            .WithDescription("Remove o apoio e publica o evento 'apoio.removido' com o novo total.")
            .RequireAuthorization(Politicas.CidadaoOnly)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithOpenApi();

        app.MapGet("/ocorrencias/{ocorrenciaId:long}/apoios", ObterResumoAsync)
            .WithName("ObterResumoApoios")
            .WithTags(Tag)
            .WithSummary("Total de apoios e se o usuário autenticado já apoiou")
            .WithDescription("Os links HATEOAS indicam ao app qual botão exibir: 'apoiar' (POST) ou 'retirar-apoio' (DELETE).")
            .RequireAuthorization()
            .Produces<ResourceEnvelope<ResumoApoiosResponse>>()
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> RegistrarAsync(
        RegistrarApoioRequest request, ClaimsPrincipal usuario, IApoioServico servico, IGeradorLinks links, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.RegistrarAsync(request, usuario.ObterUsuario(), ct);
        return resultado.ParaHttp(http, apoio => Results.Created(
            $"/ocorrencias/{apoio.OcorrenciaId}/apoios",
            new ResourceEnvelope<ApoioResponse>(apoio, links.ParaApoio(apoio))));
    }

    private static async Task<IResult> RetirarAsync(long ocorrenciaId, ClaimsPrincipal usuario, IApoioServico servico, HttpContext http, CancellationToken ct)
    {
        var resultado = await servico.RetirarAsync(ocorrenciaId, usuario.ObterUsuario(), ct);
        return resultado.ParaNoContent(http);
    }

    private static async Task<IResult> ObterResumoAsync(long ocorrenciaId, ClaimsPrincipal usuario, IApoioServico servico, IGeradorLinks links, CancellationToken ct)
    {
        var resumo = await servico.ObterResumoAsync(ocorrenciaId, usuario.ObterUsuario(), ct);
        return Results.Ok(new ResourceEnvelope<ResumoApoiosResponse>(resumo, links.ParaResumoApoios(resumo)));
    }
}
