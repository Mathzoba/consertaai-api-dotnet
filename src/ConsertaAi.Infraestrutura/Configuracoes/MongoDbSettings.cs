namespace ConsertaAi.Infraestrutura.Configuracoes;

/// <summary>Seção "MongoDbSettings" (mesmo nome usado nas aulas de MongoDB).</summary>
public sealed class MongoDbSettings
{
    public const string Secao = "MongoDbSettings";

    /// <summary>Ex.: mongodb+srv://USUARIO:SENHA@cluster.mongodb.net — SEMPRE via variável de ambiente/user-secrets.</summary>
    public string? ConnectionString { get; set; }
    public string DatabaseName { get; set; } = "ConsertaAiDb";
    public string CollectionAtividades { get; set; } = "atividades";

    public bool Habilitado => !string.IsNullOrWhiteSpace(ConnectionString);
}
