using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Infraestrutura.Configuracoes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ConsertaAi.Infraestrutura.Mongo;

/// <summary>
/// Mantém o MongoClient (thread-safe, deve ser único na aplicação), o database e a collection
/// de atividades. Cria os índices uma única vez, na primeira utilização.
/// </summary>
public sealed class MongoContexto
{
    private readonly ILogger<MongoContexto> _logger;
    private readonly Lazy<Task> _indices;

    public MongoContexto(IOptions<MongoDbSettings> opcoes, ILogger<MongoContexto> logger)
    {
        _logger = logger;
        var settings = opcoes.Value;

        if (!settings.Habilitado)
            throw new InvalidOperationException("A ConnectionString do MongoDB não foi configurada.");

        var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        clientSettings.ConnectTimeout = TimeSpan.FromSeconds(5);

        Client = new MongoClient(clientSettings);
        Database = Client.GetDatabase(settings.DatabaseName);
        Atividades = Database.GetCollection<Atividade>(settings.CollectionAtividades);

        _indices = new Lazy<Task>(CriarIndicesAsync);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public IMongoCollection<Atividade> Atividades { get; }

    public Task GarantirIndicesAsync() => _indices.Value;

    private async Task CriarIndicesAsync()
    {
        try
        {
            var indice = Builders<Atividade>.IndexKeys
                .Ascending(a => a.OcorrenciaId)
                .Descending(a => a.DataRegistro);

            await Atividades.Indexes.CreateOneAsync(
                new CreateIndexModel<Atividade>(indice, new CreateIndexOptions { Name = "ix_ocorrencia_data" }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível criar os índices da collection de atividades");
        }
    }
}
