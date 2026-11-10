using System.Diagnostics;
using ConsertaAi.Infraestrutura.Mongo;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace ConsertaAi.Infraestrutura.Health;

/// <summary>Executa o comando "ping" no MongoDB (Atlas ou container).</summary>
public class MongoDbHealthCheck : IHealthCheck
{
    private readonly MongoContexto _contexto;

    public MongoDbHealthCheck(MongoContexto contexto) => _contexto = contexto;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            await _contexto.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB respondeu ao ping.",
                new Dictionary<string, object> { ["LatenciaMs"] = cronometro.ElapsedMilliseconds, ["Database"] = _contexto.Database.DatabaseNamespace.DatabaseName });
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "MongoDB indisponível: a linha do tempo de atividades está fora do ar.", ex);
        }
    }
}
