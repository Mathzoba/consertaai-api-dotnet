using System.ComponentModel.DataAnnotations;
using ConsertaAi.Api.Filtros;
using ConsertaAi.Api.Seguranca;

namespace ConsertaAi.Api.Endpoints;

/// <summary>Corpo do POST /auth/token-teste.</summary>
/// <param name="UsuarioId">Id do usuário (vira a claim "sub"). Ex.: "1".</param>
/// <param name="Nome">Nome exibido.</param>
/// <param name="Perfil">CIDADAO ou GESTOR.</param>
public record TokenTesteRequest(
    [property: Required(ErrorMessage = "O usuarioId é obrigatório.")]
    [property: StringLength(64, MinimumLength = 1)]
    string UsuarioId,
    [property: Required(ErrorMessage = "O nome é obrigatório.")]
    [property: StringLength(100, MinimumLength = 2)]
    string Nome,
    [property: Required(ErrorMessage = "O perfil é obrigatório.")]
    [property: RegularExpression("^(CIDADAO|GESTOR)$", ErrorMessage = "O perfil deve ser CIDADAO ou GESTOR.")]
    string Perfil);

/// <summary>Token emitido para testes.</summary>
public record TokenResponse(string AccessToken, string TokenType, DateTime ExpiraEm);

/// <summary>
/// Emissor de token SOMENTE PARA TESTES (habilitado por Auth:EmissorTokenTesteHabilitado).
/// No fluxo real o token vem do POST /auth/login da API Java; esta rota permite demonstrar e testar
/// a API .NET de forma independente, com o mesmo formato de token (sub + role, HS256).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, IConfiguration configuracao)
    {
        if (!configuracao.GetValue("Auth:EmissorTokenTesteHabilitado", false))
            return app;

        app.MapPost("/auth/token-teste", (TokenTesteRequest request, TokenService tokenService) =>
            {
                var (token, expiraEm) = tokenService.GerarToken(request.UsuarioId, request.Nome, request.Perfil);
                return Results.Ok(new TokenResponse(token, "Bearer", expiraEm));
            })
            .WithName("GerarTokenTeste")
            .WithTags("Autenticação (testes)")
            .WithSummary("Gera um JWT de teste (CIDADAO ou GESTOR)")
            .WithDescription("Somente para testes/demonstração. Em produção o token é emitido pela API Java (POST /auth/login) e validado aqui com o mesmo segredo.")
            .AllowAnonymous()
            .Validar<TokenTesteRequest>()
            .Produces<TokenResponse>()
            .WithOpenApi();

        return app;
    }
}
