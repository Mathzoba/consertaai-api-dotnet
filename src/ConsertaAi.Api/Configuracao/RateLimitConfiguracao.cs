using System.Globalization;
using System.Threading.RateLimiting;
using ConsertaAi.Api.Respostas;
using ConsertaAi.Api.Seguranca;
using ConsertaAi.Aplicacao.Observabilidade;

namespace ConsertaAi.Api.Configuracao;

/// <summary>Opções da seção "RateLimit".</summary>
public sealed class RateLimitOptions
{
    public const string Secao = "RateLimit";

    /// <summary>Máximo de POSTs (apoios/comentários) por usuário dentro da janela. Plano: 5 por minuto.</summary>
    public int EscritasPorJanela { get; set; } = 5;

    /// <summary>Máximo de requisições gerais por usuário/IP dentro da janela (proteção contra abuso).</summary>
    public int RequisicoesGeraisPorJanela { get; set; } = 120;

    public int JanelaSegundos { get; set; } = 60;
}

/// <summary>
/// Rate Limiting nativo do ASP.NET Core (Microsoft.AspNetCore.RateLimiting):
/// janela fixa por usuário autenticado (ou por IP), resposta HTTP 429 com header Retry-After.
/// </summary>
public static class RateLimitConfiguracao
{
    /// <summary>Policy aplicada aos POSTs de participação (5 por minuto por usuário).</summary>
    public const string PoliticaEscrita = "escrita-por-usuario";

    public static IServiceCollection AddRateLimitingConsertaAi(this IServiceCollection services, IConfiguration configuracao)
    {
        var opcoes = configuracao.GetSection(RateLimitOptions.Secao).Get<RateLimitOptions>() ?? new RateLimitOptions();
        var janela = TimeSpan.FromSeconds(Math.Max(1, opcoes.JanelaSegundos));

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Policy nomeada: 5 escritas por minuto POR USUÁRIO (partição pela claim sub do JWT)
            limiter.AddPolicy(PoliticaEscrita, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ChaveParticao(contexto),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = opcoes.EscritasPorJanela,
                        Window = janela,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            // Limitador global: protege todas as rotas (exceto health e Swagger) contra uso excessivo
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                EhRotaLivre(contexto.Request.Path)
                    ? RateLimitPartition.GetNoLimiter("livre")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "global:" + ChaveParticao(contexto),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = opcoes.RequisicoesGeraisPorJanela,
                            Window = janela,
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));

            // Resposta 429 padronizada com Retry-After
            limiter.OnRejected = async (rejeicao, ct) =>
            {
                var http = rejeicao.HttpContext;
                var segundos = rejeicao.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                    : (int)janela.TotalSeconds;

                http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);

                AplicacaoMetricas.RateLimitRejeicoesContador.Add(1, AplicacaoMetricas.Tag("rota", http.Request.Path.ToString()));
                http.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(nameof(RateLimitConfiguracao))
                    .LogWarning("Rate limit excedido por {Particao} em {Metodo} {Rota}. Retry-After: {Segundos}s",
                        ChaveParticao(http), http.Request.Method, http.Request.Path, segundos);

                await Problemas.EscreverAsync(http, StatusCodes.Status429TooManyRequests, "Muitas requisições",
                    $"Limite de requisições excedido. Tente novamente em {segundos} segundo(s).");
            };
        });

        return services;
    }

    private static string ChaveParticao(HttpContext contexto) =>
        contexto.User.Identity?.IsAuthenticated == true && contexto.User.ObterId() is { } id
            ? $"usuario:{id}"
            : $"ip:{contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido"}";

    private static bool EhRotaLivre(PathString caminho) =>
        caminho.StartsWithSegments("/health") || caminho.StartsWithSegments("/swagger");
}
