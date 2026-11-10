namespace ConsertaAi.Infraestrutura.Configuracoes;

/// <summary>Seção "BancoDados" do appsettings. A connection string vem de ConnectionStrings:ConsertaAi (variável de ambiente).</summary>
public sealed class BancoDadosOptions
{
    public const string Secao = "BancoDados";
    public const string NomeConnectionString = "ConsertaAi";

    public ProvedorBancoDados Provedor { get; set; } = ProvedorBancoDados.Oracle;

    /// <summary>Aplica migrations (Oracle) ou cria o schema (SQL Server/InMemory) ao iniciar.</summary>
    public bool InicializarAoIniciar { get; set; } = true;

    public int TentativasConexao { get; set; } = 10;
    public int SegundosEntreTentativas { get; set; } = 5;
}
