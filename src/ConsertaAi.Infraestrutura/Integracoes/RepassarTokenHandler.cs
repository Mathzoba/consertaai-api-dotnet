using Microsoft.AspNetCore.Http;

namespace ConsertaAi.Infraestrutura.Integracoes;

/// <summary>Repassa o "Authorization: Bearer" da requisição atual para a API Java (mesmo JWT nas duas APIs).</summary>
public sealed class RepassarTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RepassarTokenHandler(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var autorizacao = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(autorizacao) && request.Headers.Authorization is null)
            request.Headers.TryAddWithoutValidation("Authorization", autorizacao);

        return base.SendAsync(request, cancellationToken);
    }
}
