using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaAi.Tests.Integration.Infraestrutura;
using FluentAssertions;

namespace ConsertaAi.Tests.Integration.Endpoints;

/// <summary>Health Checks, Swagger/OpenAPI, Correlation ID e emissor de token de testes.</summary>
public class InfraestruturaEndpointsTests : IClassFixture<ConsertaAiApiFactory>
{
    private readonly ConsertaAiApiFactory _factory;

    public InfraestruturaEndpointsTests(ConsertaAiApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetHealth_BancoDisponivel_DeveRetornar200Healthy()
    {
        // Arrange
        var cliente = _factory.CreateClient();

        // Act
        var resposta = await cliente.GetAsync("/health");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("status").GetString().Should().Be("Healthy");
        corpo.GetProperty("verificacoes").EnumerateArray().Select(v => v.GetProperty("nome").GetString()).Should().Contain("banco_dados");
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task GetHealthSondas_DeveRetornar200SemAutenticacao(string rota)
    {
        // Act
        var resposta = await _factory.CreateClient().GetAsync(rota);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSwagger_DeveDocumentarSegurancaEIdempotencyKey()
    {
        // Act
        var documento = await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");

        // Assert
        documento.Should().Contain("\"/apoios\"").And.Contain("\"/comentarios\"").And.Contain("\"/etiquetas\"");
        documento.Should().Contain("Idempotency-Key").And.Contain("bearer");
        documento.Should().Contain("\"429\"");
        documento.Should().Contain("\"/health\"").And.Contain("\"/health/ready\"").And.Contain("\"/health/live\"");
    }

    [Fact]
    public async Task QualquerRequisicao_ComCorrelationId_DeveDevolverOMesmoHeader()
    {
        // Arrange
        var cliente = _factory.CreateClient();
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        requisicao.Headers.Add("X-Correlation-ID", "teste-correlation-123");

        // Act
        var resposta = await cliente.SendAsync(requisicao);

        // Assert
        resposta.Headers.GetValues("X-Correlation-ID").Should().ContainSingle("teste-correlation-123");
    }

    [Fact]
    public async Task PostTokenTeste_PerfilValido_DeveEmitirTokenAceitoPelaApi()
    {
        // Arrange
        var cliente = _factory.CreateClient();

        // Act
        var resposta = await cliente.PostAsJsonAsync("/auth/token-teste", new { usuarioId = "777", nome = "Teste", perfil = "CIDADAO" });
        var token = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var protegido = await cliente.GetAsync("/etiquetas");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        protegido.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostTokenTeste_PerfilInexistente_DeveRetornar400()
    {
        // Act
        var resposta = await _factory.CreateClient().PostAsJsonAsync("/auth/token-teste", new { usuarioId = "1", nome = "Teste", perfil = "ADMIN" });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRotaInexistente_DeveRetornar404()
    {
        // Act
        var resposta = await _factory.CreateClient().GetAsync("/rota-que-nao-existe");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
