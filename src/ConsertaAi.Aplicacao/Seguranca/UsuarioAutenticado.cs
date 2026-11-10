namespace ConsertaAi.Aplicacao.Seguranca;

/// <summary>Papéis (roles) do Conserta Aí, idênticos aos emitidos pela API Java no token JWT.</summary>
public static class Papeis
{
    public const string Cidadao = "CIDADAO";
    public const string Gestor = "GESTOR";
}

/// <summary>
/// Usuário extraído do token JWT. A camada de aplicação recebe este objeto simples
/// em vez de depender do HttpContext (DIP: o caso de uso não conhece HTTP).
/// </summary>
public sealed record UsuarioAutenticado(string Id, string? Nome, IReadOnlyCollection<string> Papeis)
{
    public bool EhGestor => Papeis.Contains(Seguranca.Papeis.Gestor);
    public bool EhCidadao => Papeis.Contains(Seguranca.Papeis.Cidadao);
}
