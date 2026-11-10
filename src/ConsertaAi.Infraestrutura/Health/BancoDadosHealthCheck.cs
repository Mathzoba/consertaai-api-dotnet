using System.Diagnostics;
using ConsertaAi.Infraestrutura.Configuracoes;
using ConsertaAi.Infraestrutura.Dados;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ConsertaAi.Infraestrutura.Health;

/// <summary>Verifica a conectividade real com o banco relacional (Oracle ou Azure SQL) via EF Core.</summary>
public class BancoDadosHealthCheck : IHealthCheck
{
    private readonly AppDbContext _contexto;
    private readonly BancoDadosOptions _opcoes;

    public BancoDadosHealthCheck(AppDbContext contexto, IOptions<BancoDadosOptions> opcoes)
    {
        _contexto = contexto;
        _opcoes = opcoes.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            var conectou = await _contexto.Database.CanConnectAsync(cancellationToken);
            cronometro.Stop();

            var dados = new Dictionary<string, object>
            {
                ["Provedor"] = _opcoes.Provedor.ToString(),
                ["LatenciaMs"] = cronometro.ElapsedMilliseconds
            };

            return conectou
                ? HealthCheckResult.Healthy("Conexão com o banco de dados estabelecida com sucesso.", dados)
                : new HealthCheckResult(context.Registration.FailureStatus, "Não foi possível conectar ao banco de dados.", data: dados);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Falha ao conectar no banco de dados.", ex);
        }
    }
}
