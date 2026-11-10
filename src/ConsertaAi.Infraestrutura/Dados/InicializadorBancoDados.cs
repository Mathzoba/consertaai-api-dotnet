using ConsertaAi.Infraestrutura.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConsertaAi.Infraestrutura.Dados;

/// <summary>
/// Prepara o schema ao iniciar a API:
/// Oracle → aplica as Migrations do EF Core; SQL Server/InMemory → EnsureCreated.
/// Faz novas tentativas porque, em containers, o banco costuma subir depois da API.
/// </summary>
public static class InicializadorBancoDados
{
    public static async Task InicializarBancoDadosAsync(this IServiceProvider servicos, CancellationToken ct = default)
    {
        using var escopo = servicos.CreateScope();
        var opcoes = escopo.ServiceProvider.GetRequiredService<IOptions<BancoDadosOptions>>().Value;
        var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(InicializadorBancoDados));

        if (!opcoes.InicializarAoIniciar)
        {
            logger.LogInformation("Inicialização automática do banco desabilitada (BancoDados:InicializarAoIniciar=false)");
            return;
        }

        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var tentativa = 1; tentativa <= Math.Max(1, opcoes.TentativasConexao); tentativa++)
        {
            try
            {
                if (opcoes.Provedor == ProvedorBancoDados.Oracle)
                {
                    await contexto.Database.MigrateAsync(ct);
                    logger.LogInformation("Migrations do EF Core aplicadas no Oracle");
                }
                else
                {
                    await contexto.Database.EnsureCreatedAsync(ct);
                    logger.LogInformation("Schema garantido via EnsureCreated no provedor {Provedor}", opcoes.Provedor);
                }
                return;
            }
            catch (Exception ex) when (tentativa < opcoes.TentativasConexao)
            {
                logger.LogWarning(ex, "Banco indisponível (tentativa {Tentativa}/{Total}). Nova tentativa em {Segundos}s",
                    tentativa, opcoes.TentativasConexao, opcoes.SegundosEntreTentativas);
                await Task.Delay(TimeSpan.FromSeconds(opcoes.SegundosEntreTentativas), ct);
            }
            catch (Exception ex)
            {
                // A API continua no ar; o /health mostrará Unhealthy até o banco voltar
                logger.LogError(ex, "Não foi possível inicializar o banco de dados após {Total} tentativas", opcoes.TentativasConexao);
            }
        }
    }
}
