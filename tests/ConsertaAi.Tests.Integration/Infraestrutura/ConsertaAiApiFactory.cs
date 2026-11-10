using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ConsertaAi.Infraestrutura.Dados;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ConsertaAi.Tests.Integration.Infraestrutura;

/// <summary>
/// Sobe a API completa em memória (WebApplicationFactory) com banco EF Core InMemory,
/// linha do tempo em memória e publicador de eventos em log — sem dependências externas.
/// </summary>
public class ConsertaAiApiFactory : WebApplicationFactory<Program>
{
    // Valores exclusivos dos testes (não são segredos de nenhum ambiente real)
    public const string ChaveJwtTestes = "chave-exclusiva-dos-testes-de-integracao-0123456789";
    public const string Emissor = "consertaai-testes";
    public const string Audiencia = "consertaai-app-testes";

    public int EscritasPorJanela { get; init; } = 5;

    private readonly string _nomeBanco = $"consertaai-testes-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting garante que os valores existam já na fase de configuração do Program.cs
        builder.UseSetting("BancoDados:Provedor", "InMemory");
        builder.UseSetting("ConnectionStrings:ConsertaAi", _nomeBanco);
        builder.UseSetting("BancoDados:TentativasConexao", "1");
        builder.UseSetting("MongoDbSettings:ConnectionString", "");
        builder.UseSetting("Eventos:Modo", "Log");
        builder.UseSetting("ApiJava:BaseUrl", "");
        builder.UseSetting("JWT_SECRET", ChaveJwtTestes);
        builder.UseSetting("JWT_ISSUER", Emissor);
        builder.UseSetting("JWT_AUDIENCE", Audiencia);
        builder.UseSetting("Auth:EmissorTokenTesteHabilitado", "true");
        builder.UseSetting("RateLimit:EscritasPorJanela", EscritasPorJanela.ToString());
        builder.UseSetting("RateLimit:RequisicoesGeraisPorJanela", "10000");
        builder.UseSetting("RateLimit:JanelaSegundos", "60");
        builder.UseSetting("Observabilidade:ExportarConsole", "false");
        builder.UseSetting("Observabilidade:LogArquivoHabilitado", "false");
    }

    /// <summary>Cliente HTTP já autenticado com um token no formato emitido pela API .NET.</summary>
    public HttpClient CriarCliente(string usuarioId, string perfil)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GerarToken(usuarioId, perfil));
        return cliente;
    }

    public static string GerarToken(string usuarioId, string perfil, DateTime? expiraEm = null, string chave = ChaveJwtTestes) =>
        Assinar([new Claim(JwtRegisteredClaimNames.Sub, usuarioId), new Claim(ClaimTypes.Name, $"Usuário {usuarioId}"), new Claim(ClaimTypes.Role, perfil)],
            expiraEm ?? DateTime.UtcNow.AddMinutes(30), chave);

    /// <summary>Token no formato da API Java/Spring: claim "roles" em array e prefixo ROLE_.</summary>
    public static string GerarTokenFormatoJava(string usuarioId, params string[] roles)
    {
        var payload = new JwtPayload(Emissor, Audiencia, [new Claim(JwtRegisteredClaimNames.Sub, usuarioId)], DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddMinutes(30))
        {
            ["roles"] = roles
        };
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ChaveJwtTestes)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credenciais), payload));
    }

    private static string Assinar(IEnumerable<Claim> claims, DateTime expiraEm, string chave)
    {
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(Emissor, Audiencia, claims, notBefore: expiraEm.AddHours(-1), expires: expiraEm, signingCredentials: credenciais);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Executa uma consulta direta no banco (equivalente ao SELECT de conferência).</summary>
    public async Task<T> ConsultarBancoAsync<T>(Func<AppDbContext, Task<T>> consulta)
    {
        using var escopo = Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        return await consulta(contexto);
    }
}

public static class HttpClientExtensions
{
    /// <summary>POST com o header Idempotency-Key.</summary>
    public static Task<HttpResponseMessage> PostIdempotenteAsync<T>(this HttpClient cliente, string url, T corpo, string? chave = null)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        return cliente.SendAsync(requisicao);
    }
}
