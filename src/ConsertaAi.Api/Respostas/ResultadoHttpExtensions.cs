using ConsertaAi.Aplicacao.Comum;

namespace ConsertaAi.Api.Respostas;

/// <summary>Converte Resultado/Resultado&lt;T&gt; em IResult, centralizando o mapeamento de erros → HTTP.</summary>
public static class ResultadoHttpExtensions
{
    public static IResult ParaHttp<T>(this Resultado<T> resultado, HttpContext contexto, Func<T, IResult> sucesso) =>
        resultado.Sucesso ? sucesso(resultado.Valor!) : resultado.Erro!.ParaHttp(contexto);

    public static IResult ParaNoContent(this Resultado resultado, HttpContext contexto) =>
        resultado.Sucesso ? Results.NoContent() : resultado.Erro!.ParaHttp(contexto);
}
