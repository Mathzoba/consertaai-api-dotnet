using ConsertaAi.Infraestrutura.Mensageria;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConsertaAi.Infraestrutura.Health;

/// <summary>Verifica se a conexão com o RabbitMQ está aberta (eventos apoio.registrado).</summary>
public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConexao _conexao;

    public RabbitMqHealthCheck(RabbitMqConexao conexao) => _conexao = conexao;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var conexao = _conexao.ObterConexao();
            return Task.FromResult(conexao.IsOpen
                ? HealthCheckResult.Healthy("Conexão com o RabbitMQ aberta.", new Dictionary<string, object> { ["Exchange"] = _conexao.Opcoes.Exchange })
                : new HealthCheckResult(context.Registration.FailureStatus, "Conexão com o RabbitMQ fechada."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ indisponível: eventos de apoio não estão sendo entregues.", ex));
        }
    }
}
