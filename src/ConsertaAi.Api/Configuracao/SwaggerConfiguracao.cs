using System.Reflection;
using System.Xml.XPath;
using ConsertaAi.Api.Middlewares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ConsertaAi.Api.Configuracao;

/// <summary>OpenAPI/Swagger: documentação interativa com botão Authorize (Bearer) e header Idempotency-Key.</summary>
public static class SwaggerConfiguracao
{
    public static IServiceCollection AddSwaggerConsertaAi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Conserta Aí — API .NET (Participação Cidadã)",
                Version = "v1",
                Description =
                    "Backend .NET do Conserta Aí (Global Solution 2026/2 — CitizenTech). " +
                    "Cidadãos apoiam ocorrências de zeladoria já reportadas (em vez de duplicá-las) e comentam; " +
                    "gestores moderam e mantêm as etiquetas. Autentique com o mesmo JWT emitido pela API Java " +
                    "(ou, em testes, por POST /auth/token-teste) e clique em **Authorize**."
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe apenas o token JWT."
            });

            c.OperationFilter<SegurancaOperationFilter>();
            c.OperationFilter<IdempotencyKeyOperationFilter>();
            c.DocumentFilter<HealthChecksDocumentFilter>();
            c.SupportNonNullableReferenceTypes();

            foreach (var assembly in new[] { Assembly.GetExecutingAssembly(), typeof(Aplicacao.DependencyInjection).Assembly })
            {
                var xml = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (!File.Exists(xml)) continue;

                // Carrega o XML por stream (evita erro de URI em caminhos longos do Windows)
                var documento = new XPathDocument(new StringReader(File.ReadAllText(xml)));
                c.IncludeXmlComments(() => documento);
            }
        });

        return services;
    }
}

/// <summary>Adiciona o cadeado (Bearer) e as respostas 401/403 apenas nos endpoints protegidos.</summary>
public sealed class SegurancaOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadados = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadados.OfType<IAllowAnonymous>().Any() || !metadados.OfType<IAuthorizeData>().Any())
            return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
        });

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Não autenticado: token ausente, inválido ou expirado." });

        if (metadados.OfType<IAuthorizeData>().Any(a => !string.IsNullOrEmpty(a.Policy) || !string.IsNullOrEmpty(a.Roles)))
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Autenticado, mas o perfil não tem permissão." });

        operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Limite de requisições excedido (header Retry-After)." });
    }
}

/// <summary>Documenta o header obrigatório Idempotency-Key nos POSTs idempotentes.</summary>
public sealed class IdempotencyKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<RequerIdempotenciaMetadata>().Any())
            return;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotenciaExtensions.Header,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Identificador único da operação (ex.: um UUID gerado pelo app). Repetir a mesma chave com o mesmo corpo devolve a mesma resposta sem gravar de novo.",
            Schema = new OpenApiSchema { Type = "string", MaxLength = 100, Example = new OpenApiString(Guid.NewGuid().ToString()) }
        });

        operation.Responses.TryAdd("422", new OpenApiResponse { Description = "Idempotency-Key reutilizada com outro corpo." });
    }
}
