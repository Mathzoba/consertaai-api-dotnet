using ConsertaAi.Aplicacao.Observabilidade;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace ConsertaAi.Api.Configuracao;

/// <summary>Opções da seção "Observabilidade".</summary>
public sealed class ObservabilidadeOptions
{
    public const string Secao = "Observabilidade";

    /// <summary>Exporta traces e métricas no console (ConsoleExporter, para inspeção em desenvolvimento).</summary>
    public bool ExportarConsole { get; set; } = true;

    /// <summary>Grava logs em arquivo com rotação diária.</summary>
    public bool LogArquivoHabilitado { get; set; } = true;

    public string CaminhoLogArquivo { get; set; } = "logs/consertaai-.log";
}

/// <summary>Serilog (logs estruturados) + OpenTelemetry (tracing distribuído e métricas).</summary>
public static class ObservabilidadeConfiguracao
{
    /// <summary>Template do console com o CorrelationId (mesmo formato da Aula 02).</summary>
    private const string TemplateConsole = "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";

    public static WebApplicationBuilder AddSerilogConsertaAi(this WebApplicationBuilder builder)
    {
        var opcoes = builder.Configuration.GetSection(ObservabilidadeOptions.Secao).Get<ObservabilidadeOptions>() ?? new ObservabilidadeOptions();

        // Substitui o provedor de logging padrão da Microsoft pelo Serilog
        builder.Host.UseSerilog((contexto, servicos, configuracao) =>
        {
            configuracao
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                // Habilita a captura de propriedades injetadas pelo LogContext (ex.: CorrelationId)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Aplicacao", AplicacaoMetricas.NomeServico)
                .Enrich.WithProperty("Ambiente", contexto.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(outputTemplate: TemplateConsole);

            if (opcoes.LogArquivoHabilitado)
            {
                // Arquivo local com rotação diária, em JSON (chave-valor) para facilitar buscas
                configuracao.WriteTo.File(
                    formatter: new JsonFormatter(renderMessage: true),
                    path: opcoes.CaminhoLogArquivo,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7);
            }
        });

        return builder;
    }

    public static IServiceCollection AddOpenTelemetryConsertaAi(this IServiceCollection services, IConfiguration configuracao)
    {
        var opcoes = configuracao.GetSection(ObservabilidadeOptions.Secao).Get<ObservabilidadeOptions>() ?? new ObservabilidadeOptions();
        var otlpConfigurado = !string.IsNullOrWhiteSpace(configuracao["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(AplicacaoMetricas.NomeServico, serviceVersion: "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    // Auto-instrumentação das requisições HTTP recebidas (ignorando o /health)
                    .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                    // Auto-instrumentação das chamadas HTTP de saída (API Java)
                    .AddHttpClientInstrumentation()
                    // Escuta os Spans customizados dos casos de uso (RegistrarApoio, CriarComentario...)
                    .AddSource(AplicacaoMetricas.NomeServico);

                if (opcoes.ExportarConsole) tracing.AddConsoleExporter();
                if (otlpConfigurado) tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Escuta o Meter customizado (apoios_registrados_total, rate_limit_rejeicoes_total...)
                    .AddMeter(AplicacaoMetricas.NomeServico);

                if (opcoes.ExportarConsole) metrics.AddConsoleExporter();
                if (otlpConfigurado) metrics.AddOtlpExporter();
            });

        return services;
    }
}
