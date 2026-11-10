using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaAi.Aplicacao.Seguranca;
using ConsertaAi.Tests.Integration.Infraestrutura;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConsertaAi.Tests.Integration.Endpoints;

/// <summary>
/// Testes de integração dos endpoints de apoio: 201, 401, 403, 409, 400, 422, 429,
/// idempotência (uma linha só no banco) e HATEOAS.
/// Cada teste usa usuário/ocorrência próprios, para não interferir nos demais.
/// </summary>
public class ApoioEndpointsTests : IClassFixture<ConsertaAiApiFactory>
{
    private readonly ConsertaAiApiFactory _factory;

    public ApoioEndpointsTests(ConsertaAiApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PostApoio_SemToken_DeveRetornar401()
    {
        // Arrange
        var cliente = _factory.CreateClient();

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resposta.Headers.WwwAuthenticate.Should().ContainSingle(h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task PostApoio_TokenAssinadoComOutraChave_DeveRetornar401()
    {
        // Arrange
        var cliente = _factory.CreateClient();
        var tokenFalso = ConsertaAiApiFactory.GerarToken("1", Papeis.Cidadao, chave: "uma-chave-totalmente-diferente-com-mais-de-32-caracteres");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenFalso);

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostApoio_TokenExpirado_DeveRetornar401()
    {
        // Arrange
        var cliente = _factory.CreateClient();
        var tokenExpirado = ConsertaAiApiFactory.GerarToken("1", Papeis.Cidadao, DateTime.UtcNow.AddMinutes(-10));
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenExpirado);

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("expirou");
    }

    [Fact]
    public async Task PostApoio_ComPerfilGestor_DeveRetornar403()
    {
        // Arrange
        var cliente = _factory.CriarCliente("gestor-403", Papeis.Gestor);

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task PostApoio_CidadaoComDadosValidos_DeveRetornar201ComLinksHateoas()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-201", Papeis.Cidadao);

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1001 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location!.ToString().Should().Be("/ocorrencias/1001/apoios");

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("data").GetProperty("totalApoios").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("links").EnumerateArray()
            .Select(l => l.GetProperty("rel").GetString())
            .Should().Contain(["self", "retirar-apoio", "comentarios", "atividades"]);
    }

    [Fact]
    public async Task PostApoio_UsuarioApoiaDuasVezes_DeveRetornar409ESemNovaLinha()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-409", Papeis.Cidadao);
        await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1002 });

        // Act: nova requisição (outra Idempotency-Key) para a mesma ocorrência
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1002 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var linhas = await _factory.ConsultarBancoAsync(db => db.Apoios.CountAsync(a => a.OcorrenciaId == 1002));
        linhas.Should().Be(1);
    }

    [Fact]
    public async Task PostApoio_MesmaIdempotencyKeyRepetida_DeveDevolverMesmaRespostaComUmaLinhaNoBanco()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-idem", Papeis.Cidadao);
        var chave = Guid.NewGuid().ToString();

        // Act
        var primeira = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1003 }, chave);
        var segunda = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1003 }, chave);

        // Assert
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        segunda.StatusCode.Should().Be(HttpStatusCode.Created);
        segunda.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle("true");
        (await segunda.Content.ReadAsStringAsync()).Should().Be(await primeira.Content.ReadAsStringAsync());

        var linhas = await _factory.ConsultarBancoAsync(db => db.Apoios.CountAsync(a => a.OcorrenciaId == 1003));
        linhas.Should().Be(1, "a repetição não pode gravar de novo");

        var registro = await _factory.ConsultarBancoAsync(db => db.RegistrosIdempotencia.SingleAsync(r => r.Chave == chave));
        registro.StatusResposta.Should().Be(201);
    }

    [Fact]
    public async Task PostApoio_MesmaChaveComCorpoDiferente_DeveRetornar422()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-422", Papeis.Cidadao);
        var chave = Guid.NewGuid().ToString();
        await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1004 }, chave);

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1005 }, chave);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var linhas = await _factory.ConsultarBancoAsync(db => db.Apoios.CountAsync(a => a.OcorrenciaId == 1005));
        linhas.Should().Be(0);
    }

    [Fact]
    public async Task PostApoio_MesmaChaveComJsonFormatadoDiferente_DeveRepetirRespostaSemGravarDeNovo()
    {
        // Arrange: Swagger envia o JSON indentado; o app pode enviar compacto — o conteúdo é o mesmo
        var cliente = _factory.CriarCliente("cidadao-formatacao", Papeis.Cidadao);
        var chave = Guid.NewGuid().ToString();

        HttpRequestMessage Requisicao(string json)
        {
            var r = new HttpRequestMessage(HttpMethod.Post, "/apoios") { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
            r.Headers.Add("Idempotency-Key", chave);
            return r;
        }

        // Act
        var compacto = await cliente.SendAsync(Requisicao("{\"ocorrenciaId\":1010}"));
        var indentado = await cliente.SendAsync(Requisicao("{\n  \"ocorrenciaId\": 1010\n}"));

        // Assert
        compacto.StatusCode.Should().Be(HttpStatusCode.Created);
        indentado.StatusCode.Should().Be(HttpStatusCode.Created);
        indentado.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle("true");
        var linhas = await _factory.ConsultarBancoAsync(db => db.Apoios.CountAsync(a => a.OcorrenciaId == 1010));
        linhas.Should().Be(1);
    }

    [Fact]
    public async Task PostApoio_SemIdempotencyKey_DeveRetornar400()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-sem-chave", Papeis.Cidadao);

        // Act
        var resposta = await cliente.PostAsJsonAsync("/apoios", new { ocorrenciaId = 1006 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("Idempotency-Key");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task PostApoio_OcorrenciaInvalida_DeveRetornar400ComErroDeCampo(long ocorrenciaId)
    {
        // Arrange
        var cliente = _factory.CriarCliente($"cidadao-400-{ocorrenciaId}", Papeis.Cidadao);

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("errors").TryGetProperty("ocorrenciaId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostApoio_SeisRequisicoesNoMesmoMinuto_DeveRetornar429ComRetryAfter()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-rate-limit", Papeis.Cidadao);

        // Act: 5 permitidas + 1 excedente
        var status = new List<HttpStatusCode>();
        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 6; i++)
        {
            ultima = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 2000 + i });
            status.Add(ultima.StatusCode);
        }

        // Assert
        status.Take(5).Should().AllBeEquivalentTo(HttpStatusCode.Created);
        status.Last().Should().Be(HttpStatusCode.TooManyRequests);
        ultima!.Headers.RetryAfter.Should().NotBeNull();
        var linhas = await _factory.ConsultarBancoAsync(db => db.Apoios.CountAsync(a => a.OcorrenciaId == 2005));
        linhas.Should().Be(0, "a requisição bloqueada não chega ao banco");
    }

    [Fact]
    public async Task DeleteApoio_AposApoiar_DeveRetornar204EDepois404()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-delete", Papeis.Cidadao);
        await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1007 });

        // Act
        var primeira = await cliente.DeleteAsync("/apoios/1007");
        var segunda = await cliente.DeleteAsync("/apoios/1007");

        // Assert
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);
        segunda.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetResumo_LinksHateoas_DevemMudarConformeOEstadoDoApoio()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-resumo", Papeis.Cidadao);

        // Act
        var antes = await cliente.GetFromJsonAsync<JsonElement>("/ocorrencias/1008/apoios");
        await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1008 });
        var depois = await cliente.GetFromJsonAsync<JsonElement>("/ocorrencias/1008/apoios");

        // Assert
        Rels(antes).Should().Contain("apoiar").And.NotContain("retirar-apoio");
        Rels(depois).Should().Contain("retirar-apoio").And.NotContain("apoiar");
        depois.GetProperty("data").GetProperty("usuarioApoiou").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task PostApoio_TokenNoFormatoDaApiJava_DeveSerAceito()
    {
        // Arrange: "roles": ["ROLE_CIDADAO"] como o Spring Security costuma emitir
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            ConsertaAiApiFactory.GerarTokenFormatoJava("555", "ROLE_CIDADAO"));

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 1009 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var apoio = await _factory.ConsultarBancoAsync(db => db.Apoios.SingleAsync(a => a.OcorrenciaId == 1009));
        apoio.UsuarioId.Should().Be("555");
    }

    private static IEnumerable<string?> Rels(JsonElement envelope) =>
        envelope.GetProperty("links").EnumerateArray().Select(l => l.GetProperty("rel").GetString());
}
