namespace ConsertaAi.Api.Seguranca;

/// <summary>
/// Configuração do JWT compartilhado com a API Java (mesmo segredo, issuer e audience).
/// Lida das variáveis de ambiente JWT_SECRET, JWT_ISSUER e JWT_AUDIENCE (contrato do grupo)
/// ou, alternativamente, da seção "Jwt" (Chave/Emissor/Audiencia, nomes usados na aula).
/// O segredo NUNCA fica no repositório: use variável de ambiente, user-secrets ou variável secreta da pipeline.
/// </summary>
public sealed class JwtOptions
{
    public const int TamanhoMinimoChave = 32;

    public string Chave { get; init; } = string.Empty;
    public string Emissor { get; init; } = string.Empty;
    public string Audiencia { get; init; } = string.Empty;
    public int ExpiracaoMinutos { get; init; } = 60;

    /// <summary>Indica que a chave foi gerada aleatoriamente para o ambiente de desenvolvimento.</summary>
    public bool ChaveTemporaria { get; init; }

    public static JwtOptions Carregar(IConfiguration configuracao, IHostEnvironment ambiente)
    {
        var chave = Primeiro(configuracao["JWT_SECRET"], configuracao["Jwt:Chave"]);
        var temporaria = false;

        if (string.IsNullOrWhiteSpace(chave))
        {
            if (!ambiente.IsDevelopment())
                throw new InvalidOperationException(
                    "JWT_SECRET não configurado. Defina a variável de ambiente JWT_SECRET (mesmo segredo da API Java, mínimo de 32 caracteres).");

            // Somente em Development: chave aleatória por execução (tokens só valem até reiniciar a API)
            chave = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
            temporaria = true;
        }

        if (chave.Length < TamanhoMinimoChave)
            throw new InvalidOperationException($"JWT_SECRET deve ter no mínimo {TamanhoMinimoChave} caracteres (exigência do HS256).");

        return new JwtOptions
        {
            Chave = chave,
            Emissor = Primeiro(configuracao["JWT_ISSUER"], configuracao["Jwt:Emissor"]) ?? "consertaai",
            Audiencia = Primeiro(configuracao["JWT_AUDIENCE"], configuracao["Jwt:Audiencia"]) ?? "consertaai-app",
            ExpiracaoMinutos = int.TryParse(configuracao["Jwt:ExpiracaoMinutos"], out var minutos) ? minutos : 60,
            ChaveTemporaria = temporaria
        };
    }

    private static string? Primeiro(params string?[] valores) => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
