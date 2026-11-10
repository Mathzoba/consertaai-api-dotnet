using System.Security.Claims;
using ConsertaAi.Aplicacao.Seguranca;

namespace ConsertaAi.Api.Seguranca;

/// <summary>Converte o usuário do token (ClaimsPrincipal) no objeto simples usado pela camada de Aplicação.</summary>
public static class ClaimsPrincipalExtensions
{
    public static string? ObterId(this ClaimsPrincipal usuario) =>
        usuario.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? usuario.FindFirstValue("sub");

    public static UsuarioAutenticado ObterUsuario(this ClaimsPrincipal usuario)
    {
        var id = usuario.ObterId()
                 ?? throw new UnauthorizedAccessException("O token não possui a claim 'sub' (id do usuário).");

        var nome = usuario.FindFirstValue(ClaimTypes.Name)
                   ?? usuario.FindFirstValue("name")
                   ?? usuario.FindFirstValue("nome")
                   ?? usuario.FindFirstValue(ClaimTypes.Email);

        var papeis = usuario.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList();
        return new UsuarioAutenticado(id, nome, papeis);
    }

    /// <summary>
    /// Normaliza papéis vindos de emissores diferentes: "roles" (API Java), "role" ou ClaimTypes.Role,
    /// com ou sem o prefixo "ROLE_" do Spring Security. Resultado: CIDADAO / GESTOR em ClaimTypes.Role.
    /// </summary>
    public static void NormalizarPapeis(this ClaimsIdentity identidade)
    {
        string[] tiposAceitos = ["roles", "role", "papel", "authorities", ClaimTypes.Role];

        var papeis = identidade.Claims
            .Where(c => tiposAceitos.Contains(c.Type))
            .SelectMany(c => c.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(p => p.Trim('[', ']', '"').ToUpperInvariant())
            .Select(p => p.StartsWith("ROLE_", StringComparison.Ordinal) ? p["ROLE_".Length..] : p)
            .Where(p => p.Length > 0)
            .Distinct()
            .ToList();

        foreach (var papel in papeis.Where(p => !identidade.HasClaim(ClaimTypes.Role, p)))
            identidade.AddClaim(new Claim(ClaimTypes.Role, papel));
    }
}
