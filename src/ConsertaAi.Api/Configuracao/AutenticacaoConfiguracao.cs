using System.Security.Claims;
using System.Text;
using ConsertaAi.Api.Respostas;
using ConsertaAi.Api.Seguranca;
using ConsertaAi.Aplicacao.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ConsertaAi.Api.Configuracao;

/// <summary>Nomes das policies de autorização (contrato do plano do grupo).</summary>
public static class Politicas
{
    public const string CidadaoOnly = "CidadaoOnly";
    public const string GestorOnly = "GestorOnly";
}

/// <summary>Autenticação (COMO o token é validado) e Autorização (O QUE cada papel pode acessar).</summary>
public static class AutenticacaoConfiguracao
{
    public static IServiceCollection AddAutenticacaoJwt(this IServiceCollection services, IConfiguration configuracao, IHostEnvironment ambiente)
    {
        var jwt = JwtOptions.Carregar(configuracao, ambiente);
        services.AddSingleton(jwt);
        services.AddSingleton<TokenService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Emissor,
                    ValidAudience = jwt.Audiencia,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Chave)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    // Aceita o formato de papéis da API Java ("roles": ["CIDADAO"]) e do Spring ("ROLE_GESTOR")
                    OnTokenValidated = contexto =>
                    {
                        if (contexto.Principal?.Identity is ClaimsIdentity identidade)
                        {
                            identidade.NormalizarPapeis();
                            if (contexto.Principal.ObterId() is null)
                                contexto.Fail("O token não possui a claim 'sub' com o id do usuário.");
                        }
                        return Task.CompletedTask;
                    },

                    // 401 com corpo ProblemDetails (sem token, token inválido ou expirado)
                    OnChallenge = async contexto =>
                    {
                        contexto.HandleResponse();
                        var detalhe = contexto.AuthenticateFailure is SecurityTokenExpiredException
                            ? "O token JWT expirou. Faça login novamente."
                            : "Envie um token JWT válido no header 'Authorization: Bearer <token>'.";

                        contexto.Response.Headers.WWWAuthenticate = "Bearer";
                        await Problemas.EscreverAsync(contexto.HttpContext, StatusCodes.Status401Unauthorized, "Não autenticado", detalhe);
                    },

                    // 403 com corpo ProblemDetails (token válido, mas o papel não atende à policy)
                    OnForbidden = contexto => Problemas.EscreverAsync(contexto.HttpContext, StatusCodes.Status403Forbidden,
                        "Acesso negado", "Seu perfil não tem permissão para executar esta operação.")
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.CidadaoOnly, policy => policy.RequireAuthenticatedUser().RequireRole(Papeis.Cidadao))
            .AddPolicy(Politicas.GestorOnly, policy => policy.RequireAuthenticatedUser().RequireRole(Papeis.Gestor));

        return services;
    }
}
