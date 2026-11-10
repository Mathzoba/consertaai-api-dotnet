using System.Text.Json.Serialization;
using ConsertaAi.Api.Configuracao;
using ConsertaAi.Api.Endpoints;
using ConsertaAi.Api.Hateoas;
using ConsertaAi.Api.Middlewares;
using ConsertaAi.Api.Respostas;
using ConsertaAi.Aplicacao;
using ConsertaAi.Aplicacao.Moderacao;
using ConsertaAi.Infraestrutura;
using ConsertaAi.Infraestrutura.Dados;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

// ======================================================================================
// Conserta Aí — API .NET (Minimal API) — camada de participação cidadã
// Domínio → Aplicação → Infraestrutura → API (Clean Architecture + SOLID)
// ======================================================================================

var builder = WebApplication.CreateBuilder(args);

// ---------- Observabilidade: Serilog (logs estruturados) + OpenTelemetry (traces e métricas)
builder.AddSerilogConsertaAi();
builder.Services.AddOpenTelemetryConsertaAi(builder.Configuration);

// ---------- Erros padronizados (ProblemDetails + tratador global de exceções)
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx => Problemas.Enriquecer(ctx.HttpContext, ctx.ProblemDetails));
builder.Services.AddExceptionHandler<TratadorExcecoesGlobal>();

// ---------- JSON: enums como texto ("ApoioRegistrado" em vez de 0)
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- Camadas (Injeção de Dependência sempre por abstrações)
builder.Services.Configure<ModeracaoOptions>(builder.Configuration.GetSection(ModeracaoOptions.Secao));
builder.Services.AddAplicacao();
builder.Services.AddInfraestrutura(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IGeradorLinks, GeradorLinks>();

// ---------- Segurança e proteção
builder.Services.AddAutenticacaoJwt(builder.Configuration, builder.Environment);
builder.Services.AddRateLimitingConsertaAi(builder.Configuration);

// ---------- CORS para o app mobile (Expo) e Expo Web
var origens = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
{
    if (origens.Length == 0 || origens.Contains("*")) policy.AllowAnyOrigin();
    else policy.WithOrigins(origens);

    policy.AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders(CorrelationIdMiddleware.CorrelationIdHeader, "Retry-After", IdempotenciaExtensions.HeaderRepeticao, "Location");
}));

// ---------- OpenAPI / Swagger
builder.Services.AddSwaggerConsertaAi();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// ======================================================================================
// Pipeline HTTP (a ORDEM importa)
// ======================================================================================

// 1) Correlation ID primeiro: todos os logs seguintes (inclusive o de requisição) carregam o mesmo id
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseForwardedHeaders();

// 2) Swagger habilitado também em nuvem, para a demonstração do CRUD no Azure
if (app.Configuration.GetValue("Swagger:Habilitado", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Conserta Aí — API .NET v1");
        c.DocumentTitle = "Conserta Aí — API .NET";
    });
}

// 3) Roteamento → CORS → Autenticação (quem é você?) → Autorização (você pode?) → Rate Limit → Idempotência
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<IdempotenciaMiddleware>();

// ======================================================================================
// Endpoints
// ======================================================================================
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecksConsertaAi();
app.MapAuthEndpoints(app.Configuration);
app.MapApoioEndpoints();
app.MapComentarioEndpoints();
app.MapEtiquetaEndpoints();
app.MapAtividadeEndpoints();

// Migrations (Oracle) ou EnsureCreated (Azure SQL / InMemory), com novas tentativas
await app.Services.InicializarBancoDadosAsync();

app.Run();

/// <summary>Necessário para os testes de integração (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
