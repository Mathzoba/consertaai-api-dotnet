using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ConsertaAi.Api.Respostas;

namespace ConsertaAi.Api.Filtros;

/// <summary>
/// Endpoint filter que valida o DTO de entrada com DataAnnotations antes de chegar ao caso de uso.
/// Erros retornam HTTP 400 no formato ValidationProblemDetails, com o nome do campo em camelCase.
/// </summary>
public sealed class ValidacaoFiltro<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argumento = context.Arguments.OfType<T>().FirstOrDefault();
        if (argumento is null)
            return await next(context);

        var resultados = new List<ValidationResult>();
        if (Validator.TryValidateObject(argumento, new ValidationContext(argumento), resultados, validateAllProperties: true))
            return await next(context);

        var erros = resultados
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [string.Empty])
                .Select(campo => (Campo: JsonNamingPolicy.CamelCase.ConvertName(campo), Mensagem: r.ErrorMessage ?? "Valor inválido.")))
            .GroupBy(e => e.Campo)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Mensagem).ToArray());

        return Problemas.Validacao(context.HttpContext, erros);
    }
}

public static class ValidacaoFiltroExtensions
{
    /// <summary>Atalho: .Validar&lt;MeuRequest&gt;() no mapeamento do endpoint.</summary>
    public static RouteHandlerBuilder Validar<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidacaoFiltro<T>>().ProducesValidationProblem();
}
