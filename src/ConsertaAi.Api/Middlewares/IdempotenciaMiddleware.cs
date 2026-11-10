using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConsertaAi.Api.Respostas;
using ConsertaAi.Api.Seguranca;
using ConsertaAi.Aplicacao.Observabilidade;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Interfaces;

namespace ConsertaAi.Api.Middlewares;

/// <summary>Metadado que marca um endpoint como "exige Idempotency-Key".</summary>
public sealed class RequerIdempotenciaMetadata;

/// <summary>Opções da seção "Idempotencia".</summary>
public sealed class IdempotenciaOptions
{
    public const string Secao = "Idempotencia";
    public int ValidadeHoras { get; set; } = 24;
}

public static class IdempotenciaExtensions
{
    public const string Header = "Idempotency-Key";
    public const string HeaderRepeticao = "Idempotency-Replayed";

    /// <summary>Exige o header Idempotency-Key no endpoint (POST /apoios e POST /comentarios).</summary>
    public static TBuilder RequerIdempotencia<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new RequerIdempotenciaMetadata());
}

/// <summary>
/// Implementa idempotência para POSTs marcados com <see cref="RequerIdempotenciaMetadata"/>:
/// <list type="bullet">
/// <item>Chave nova: executa a requisição e salva status + corpo da resposta na tabela IDEMPOTENCY_KEY.</item>
/// <item>Chave repetida (mesmo usuário, endpoint e corpo): devolve a MESMA resposta sem gravar de novo.</item>
/// <item>Mesma chave com corpo diferente: 422 Unprocessable Entity.</item>
/// <item>Mesma chave ainda em processamento (requisições simultâneas): 409 Conflict.</item>
/// </list>
/// </summary>
public class IdempotenciaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotenciaMiddleware> _logger;
    private readonly TimeSpan _validade;

    public IdempotenciaMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, IConfiguration configuracao, ILogger<IdempotenciaMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
        var opcoes = configuracao.GetSection(IdempotenciaOptions.Secao).Get<IdempotenciaOptions>() ?? new IdempotenciaOptions();
        _validade = TimeSpan.FromHours(Math.Max(1, opcoes.ValidadeHoras));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequerIdempotenciaMetadata>() is null)
        {
            await _next(context);
            return;
        }

        var chave = context.Request.Headers[IdempotenciaExtensions.Header].ToString().Trim();
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > RegistroIdempotencia.TamanhoMaximoChave)
        {
            await Problemas.EscreverAsync(context, StatusCodes.Status400BadRequest, "Header Idempotency-Key obrigatório",
                $"Envie o header '{IdempotenciaExtensions.Header}' com um identificador único (ex.: UUID) de até {RegistroIdempotencia.TamanhoMaximoChave} caracteres.");
            return;
        }

        var usuarioId = context.User.ObterId() ?? "anonimo";
        var endpoint = $"{context.Request.Method} {context.Request.Path}";
        var hashCorpo = await CalcularHashCorpoAsync(context.Request);

        // Escopo próprio: a gravação da idempotência não se mistura com o DbContext do endpoint
        using var escopo = _scopeFactory.CreateScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IIdempotenciaRepositorio>();

        var existente = await repositorio.ObterAsync(chave, context.RequestAborted);
        if (existente is not null && existente.Expirou(_validade, DateTime.UtcNow))
        {
            await repositorio.RemoverAsync(existente, context.RequestAborted);
            existente = null;
        }

        if (existente is not null)
        {
            await ResponderExistenteAsync(context, existente, usuarioId, endpoint, hashCorpo);
            return;
        }

        var registro = new RegistroIdempotencia(chave, usuarioId, endpoint, hashCorpo);
        if (!await repositorio.TentarReservarAsync(registro, context.RequestAborted))
        {
            await Problemas.EscreverAsync(context, StatusCodes.Status409Conflict, "Requisição em processamento",
                "Uma requisição com esta Idempotency-Key acabou de ser recebida e ainda está sendo processada.");
            return;
        }

        await ExecutarEArmazenarAsync(context, repositorio, registro);
    }

    private async Task ResponderExistenteAsync(HttpContext context, RegistroIdempotencia existente, string usuarioId, string endpoint, string hashCorpo)
    {
        if (!existente.CorrespondeA(usuarioId, endpoint, hashCorpo))
        {
            _logger.LogWarning("Idempotency-Key {Chave} reutilizada com corpo/endpoint diferente", existente.Chave);
            await Problemas.EscreverAsync(context, StatusCodes.Status422UnprocessableEntity, "Idempotency-Key reutilizada",
                "Esta Idempotency-Key já foi usada com outro corpo ou em outro endpoint. Gere uma nova chave para uma nova operação.");
            return;
        }

        if (existente.EmProcessamento)
        {
            await Problemas.EscreverAsync(context, StatusCodes.Status409Conflict, "Requisição em processamento",
                "A requisição original com esta Idempotency-Key ainda está sendo processada. Tente novamente em instantes.");
            return;
        }

        AplicacaoMetricas.IdempotenciaRepeticoesContador.Add(1, AplicacaoMetricas.Tag("endpoint", endpoint));
        _logger.LogInformation("Requisição repetida com Idempotency-Key {Chave}: devolvendo a resposta armazenada (HTTP {Status})",
            existente.Chave, existente.StatusResposta);

        context.Response.StatusCode = existente.StatusResposta;
        context.Response.Headers[IdempotenciaExtensions.HeaderRepeticao] = "true";
        if (!string.IsNullOrEmpty(existente.Localizacao))
            context.Response.Headers.Location = existente.Localizacao;
        if (!string.IsNullOrEmpty(existente.TipoConteudo))
            context.Response.ContentType = existente.TipoConteudo;
        if (!string.IsNullOrEmpty(existente.CorpoResposta))
            await context.Response.WriteAsync(existente.CorpoResposta, Encoding.UTF8);
    }

    private async Task ExecutarEArmazenarAsync(HttpContext context, IIdempotenciaRepositorio repositorio, RegistroIdempotencia registro)
    {
        var corpoOriginal = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Position = 0;
            var corpo = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();

            if (context.Response.StatusCode < StatusCodes.Status500InternalServerError)
            {
                registro.Concluir(context.Response.StatusCode, corpo, context.Response.ContentType, context.Response.Headers.Location.ToString());
                await repositorio.AtualizarAsync(registro, CancellationToken.None);
            }
            else
            {
                // Erros de servidor não ficam gravados: o cliente pode tentar de novo com a mesma chave
                await repositorio.RemoverAsync(registro, CancellationToken.None);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(corpoOriginal);
        }
        catch
        {
            await repositorio.RemoverAsync(registro, CancellationToken.None);
            throw;
        }
        finally
        {
            context.Response.Body = corpoOriginal;
        }
    }

    private static async Task<string> CalcularHashCorpoAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using var memoria = new MemoryStream();
        await request.Body.CopyToAsync(memoria);
        request.Body.Position = 0;
        return Convert.ToHexString(SHA256.HashData(NormalizarJson(memoria.ToArray())));
    }

    /// <summary>
    /// Compara o CONTEÚDO do JSON, não a formatação: espaços, quebras de linha e indentação
    /// (que mudam entre Swagger, app e Postman) não fazem a mesma requisição parecer diferente.
    /// Se o corpo não for um JSON válido, usa os bytes originais.
    /// </summary>
    private static byte[] NormalizarJson(byte[] corpo)
    {
        if (corpo.Length == 0) return corpo;
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            return JsonSerializer.SerializeToUtf8Bytes(documento.RootElement);
        }
        catch (JsonException)
        {
            return corpo;
        }
    }
}
