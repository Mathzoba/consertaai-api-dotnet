using ConsertaAi.Aplicacao.Moderacao;
using ConsertaAi.Aplicacao.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace ConsertaAi.Aplicacao;

/// <summary>Registro da camada de Aplicação no container de Injeção de Dependências (sempre por abstrações).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAplicacao(this IServiceCollection services)
    {
        services.AddScoped<IApoioServico, ApoioServico>();
        services.AddScoped<IComentarioServico, ComentarioServico>();
        services.AddScoped<IEtiquetaServico, EtiquetaServico>();
        services.AddScoped<IAtividadeServico, AtividadeServico>();

        // Strategy: cada regra de moderação é registrada como uma implementação de IRegraModeracao
        services.AddSingleton<IRegraModeracao, RegraTamanhoTexto>();
        services.AddSingleton<IRegraModeracao, RegraLinguagemOfensiva>();
        services.AddSingleton<IRegraModeracao, RegraAntiSpam>();
        services.AddSingleton<IModeradorComentario, ModeradorComentario>();

        return services;
    }
}
