namespace ConsertaAi.Infraestrutura.Configuracoes;

/// <summary>Seção "ApiJava": integração com a API Java (dona das ocorrências).</summary>
public sealed class ApiJavaOptions
{
    public const string Secao = "ApiJava";

    public string? BaseUrl { get; set; }
    public string HealthPath { get; set; } = "/actuator/health";

    /// <summary>Se true, consulta GET /ocorrencias/{id} na Java antes de aceitar apoio/comentário.</summary>
    public bool ValidarOcorrencias { get; set; }

    public int TimeoutSegundos { get; set; } = 5;

    public bool Configurada => !string.IsNullOrWhiteSpace(BaseUrl);
}
