using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Dominio.Interfaces;
using ConsertaAi.Infraestrutura.Configuracoes;
using ConsertaAi.Infraestrutura.Dados;
using ConsertaAi.Infraestrutura.Health;
using ConsertaAi.Infraestrutura.Integracoes;
using ConsertaAi.Infraestrutura.Mensageria;
using ConsertaAi.Infraestrutura.Mongo;
using ConsertaAi.Infraestrutura.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConsertaAi.Infraestrutura;

/// <summary>
/// Registro da Infraestrutura no container de DI. Cada dependência externa é escolhida por configuração
/// e sempre registrada pela sua abstração (DIP): trocar Oracle por Azure SQL, ou RabbitMQ por HTTP,
/// não muda nenhuma linha das camadas de Domínio e Aplicação.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Tag dos checks que indicam se a API está pronta para receber tráfego (/health/ready).</summary>
    public const string TagPronto = "pronto";

    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<BancoDadosOptions>(configuracao.GetSection(BancoDadosOptions.Secao));
        services.Configure<MongoDbSettings>(configuracao.GetSection(MongoDbSettings.Secao));
        services.Configure<RabbitMqOptions>(configuracao.GetSection(RabbitMqOptions.Secao));
        services.Configure<ApiJavaOptions>(configuracao.GetSection(ApiJavaOptions.Secao));
        services.Configure<EventosOptions>(configuracao.GetSection(EventosOptions.Secao));

        var bancoDados = configuracao.GetSection(BancoDadosOptions.Secao).Get<BancoDadosOptions>() ?? new BancoDadosOptions();
        var mongo = configuracao.GetSection(MongoDbSettings.Secao).Get<MongoDbSettings>() ?? new MongoDbSettings();
        var apiJava = configuracao.GetSection(ApiJavaOptions.Secao).Get<ApiJavaOptions>() ?? new ApiJavaOptions();
        var eventos = configuracao.GetSection(EventosOptions.Secao).Get<EventosOptions>() ?? new EventosOptions();

        var healthChecks = services.AddHealthChecks();

        AdicionarBancoRelacional(services, healthChecks, configuracao, bancoDados);
        AdicionarMongoDb(services, healthChecks, mongo);
        AdicionarApiJava(services, healthChecks, apiJava);
        AdicionarPublicadorEventos(services, healthChecks, eventos, apiJava);

        return services;
    }

    private static void AdicionarBancoRelacional(IServiceCollection services, IHealthChecksBuilder healthChecks, IConfiguration configuracao, BancoDadosOptions opcoes)
    {
        var connectionString = configuracao.GetConnectionString(BancoDadosOptions.NomeConnectionString);

        if (opcoes.Provedor != ProvedorBancoDados.InMemory && string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Defina a connection string 'ConnectionStrings:{BancoDadosOptions.NomeConnectionString}' " +
                "(variável de ambiente ConnectionStrings__ConsertaAi ou dotnet user-secrets) ou use BancoDados:Provedor=InMemory.");

        services.AddDbContext<AppDbContext>(options =>
        {
            _ = opcoes.Provedor switch
            {
                ProvedorBancoDados.Oracle => options.UseOracle(connectionString,
                    oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)),
                ProvedorBancoDados.SqlServer => options.UseSqlServer(connectionString,
                    sql => sql.EnableRetryOnFailure(maxRetryCount: 5)),
                _ => options.UseInMemoryDatabase(string.IsNullOrWhiteSpace(connectionString) ? "ConsertaAi" : connectionString)
            };
        });

        services.AddScoped<IApoioRepositorio, ApoioRepositorio>();
        services.AddScoped<IComentarioRepositorio, ComentarioRepositorio>();
        services.AddScoped<IEtiquetaRepositorio, EtiquetaRepositorio>();
        services.AddScoped<IIdempotenciaRepositorio, IdempotenciaRepositorio>();

        // Banco relacional fora do ar = API não funciona → Unhealthy (HTTP 503 no /health)
        healthChecks.AddCheck<BancoDadosHealthCheck>("banco_dados", HealthStatus.Unhealthy, [TagPronto, "banco"]);
    }

    private static void AdicionarMongoDb(IServiceCollection services, IHealthChecksBuilder healthChecks, MongoDbSettings opcoes)
    {
        if (!opcoes.Habilitado)
        {
            // Sem connection string: linha do tempo em memória (mesmo contrato — LSP)
            services.AddSingleton<IAtividadeRepositorio, MemoriaAtividadeRepositorio>();
            return;
        }

        services.AddSingleton<MongoContexto>();
        services.AddScoped<IAtividadeRepositorio, MongoAtividadeRepositorio>();

        // Mongo guarda só a linha do tempo: se cair, apoios/comentários seguem funcionando → Degraded
        healthChecks.AddCheck<MongoDbHealthCheck>("mongodb", HealthStatus.Degraded, ["nosql"]);
    }

    private static void AdicionarApiJava(IServiceCollection services, IHealthChecksBuilder healthChecks, ApiJavaOptions opcoes)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<RepassarTokenHandler>();

        var baseUrl = opcoes.Configurada ? opcoes.BaseUrl!.TrimEnd('/') + "/" : "http://localhost/";

        services.AddHttpClient(HttpApiJavaPublicadorEventos.NomeCliente, cliente =>
            {
                cliente.BaseAddress = new Uri(baseUrl);
                cliente.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos);
            })
            .AddHttpMessageHandler<RepassarTokenHandler>();

        services.AddHttpClient(ApiJavaHealthCheck.NomeCliente, cliente =>
        {
            cliente.BaseAddress = new Uri(baseUrl);
            cliente.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos);
        });

        if (opcoes.Configurada && opcoes.ValidarOcorrencias)
            services.AddScoped<IConsultaOcorrencia, HttpConsultaOcorrencia>();
        else
            services.AddSingleton<IConsultaOcorrencia, ConsultaOcorrenciaDesabilitada>();

        if (opcoes.Configurada)
            healthChecks.AddCheck<ApiJavaHealthCheck>("api_java", HealthStatus.Degraded, ["externo"]);
    }

    private static void AdicionarPublicadorEventos(IServiceCollection services, IHealthChecksBuilder healthChecks, EventosOptions eventos, ApiJavaOptions apiJava)
    {
        switch (eventos.Modo)
        {
            case ModoPublicacaoEventos.RabbitMq:
                services.AddSingleton<RabbitMqConexao>();
                services.AddSingleton<IPublicadorEventos, RabbitMqPublicadorEventos>();
                healthChecks.AddCheck<RabbitMqHealthCheck>("rabbitmq", HealthStatus.Degraded, ["mensageria"]);
                break;

            case ModoPublicacaoEventos.HttpApiJava when apiJava.Configurada:
                services.AddScoped<IPublicadorEventos, HttpApiJavaPublicadorEventos>();
                break;

            default:
                services.AddSingleton<IPublicadorEventos, LogPublicadorEventos>();
                break;
        }
    }
}
