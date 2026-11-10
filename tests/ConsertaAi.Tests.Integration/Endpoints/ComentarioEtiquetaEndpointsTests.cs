using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaAi.Aplicacao.Seguranca;
using ConsertaAi.Infraestrutura.Dados.Mapeamentos;
using ConsertaAi.Tests.Integration.Infraestrutura;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConsertaAi.Tests.Integration.Endpoints;

/// <summary>CRUD de comentários (com etiquetas N:N) e de etiquetas, incluindo regras de autoria e papéis.</summary>
public class ComentarioEtiquetaEndpointsTests : IClassFixture<ConsertaAiApiFactory>
{
    private readonly ConsertaAiApiFactory _factory;

    public ComentarioEtiquetaEndpointsTests(ConsertaAiApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PostComentario_ComEtiqueta_DeveRetornar201EGravarRelacaoNN()
    {
        // Arrange
        var cliente = _factory.CriarCliente("autor-1", Papeis.Cidadao);
        var corpo = new { ocorrenciaId = 3001, texto = "A água está acumulando no buraco", etiquetaIds = new[] { EtiquetaMapeamento.RiscoPedestresId } };

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/comentarios", corpo);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var envelope = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var id = envelope.GetProperty("data").GetProperty("id").GetGuid();
        envelope.GetProperty("data").GetProperty("etiquetas")[0].GetProperty("nome").GetString().Should().Be("Risco a pedestres");

        var relacoes = await _factory.ConsultarBancoAsync(db => db.ComentariosEtiquetas.CountAsync(ce => ce.ComentarioId == id));
        relacoes.Should().Be(1);
    }

    [Fact]
    public async Task PostComentario_TextoOfensivo_DeveRetornar400()
    {
        // Arrange
        var cliente = _factory.CriarCliente("autor-ofensivo", Papeis.Cidadao);

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/comentarios", new { ocorrenciaId = 3002, texto = "o responsável é um idiota" });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("linguagem ofensiva");
    }

    [Fact]
    public async Task PostComentario_TextoCurto_DeveRetornar400ComErroDeCampo()
    {
        // Arrange
        var cliente = _factory.CriarCliente("autor-curto", Papeis.Cidadao);

        // Act
        var resposta = await cliente.PostIdempotenteAsync("/comentarios", new { ocorrenciaId = 3003, texto = "ok" });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("errors").TryGetProperty("texto", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PutComentario_PorOutroUsuario_DeveRetornar403EPeloAutor200()
    {
        // Arrange
        var autor = _factory.CriarCliente("autor-put", Papeis.Cidadao);
        var outro = _factory.CriarCliente("intruso-put", Papeis.Cidadao);
        var criado = await (await autor.PostIdempotenteAsync("/comentarios", new { ocorrenciaId = 3004, texto = "Texto original" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = criado.GetProperty("data").GetProperty("id").GetGuid();

        // Act
        var tentativaIntruso = await outro.PutAsJsonAsync($"/comentarios/{id}", new { texto = "Texto adulterado" });
        var edicaoAutor = await autor.PutAsJsonAsync($"/comentarios/{id}", new { texto = "Texto corrigido pelo autor" });

        // Assert
        tentativaIntruso.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        edicaoAutor.StatusCode.Should().Be(HttpStatusCode.OK);
        var texto = await _factory.ConsultarBancoAsync(db => db.Comentarios.Where(c => c.Id == id).Select(c => c.Texto).SingleAsync());
        texto.Should().Be("Texto corrigido pelo autor");
    }

    [Fact]
    public async Task DeleteComentario_PorGestor_DeveRetornar204()
    {
        // Arrange
        var autor = _factory.CriarCliente("autor-delete", Papeis.Cidadao);
        var gestor = _factory.CriarCliente("gestor-delete", Papeis.Gestor);
        var criado = await (await autor.PostIdempotenteAsync("/comentarios", new { ocorrenciaId = 3005, texto = "Comentário a moderar" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = criado.GetProperty("data").GetProperty("id").GetGuid();

        // Act
        var resposta = await gestor.DeleteAsync($"/comentarios/{id}");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await autor.GetAsync($"/comentarios/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteComentario_PorOutroCidadao_DeveRetornar403()
    {
        // Arrange
        var autor = _factory.CriarCliente("autor-delete-2", Papeis.Cidadao);
        var outro = _factory.CriarCliente("intruso-delete-2", Papeis.Cidadao);
        var criado = await (await autor.PostIdempotenteAsync("/comentarios", new { ocorrenciaId = 3006, texto = "Comentário protegido" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = criado.GetProperty("data").GetProperty("id").GetGuid();

        // Act
        var resposta = await outro.DeleteAsync($"/comentarios/{id}");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetComentarios_Paginado_DeveTrazerMetadadosELinksDeNavegacao()
    {
        // Arrange
        var cliente = _factory.CriarCliente("autor-paginacao", Papeis.Cidadao);
        for (var i = 1; i <= 3; i++)
            await cliente.PostIdempotenteAsync("/comentarios", new { ocorrenciaId = 3007, texto = $"Comentário número {i}" });

        // Act
        var pagina = await cliente.GetFromJsonAsync<JsonElement>("/ocorrencias/3007/comentarios?pagina=1&tamanhoPagina=2");

        // Assert
        pagina.GetProperty("totalRegistros").GetInt64().Should().Be(3);
        pagina.GetProperty("totalPaginas").GetInt32().Should().Be(2);
        pagina.GetProperty("itens").GetArrayLength().Should().Be(2);
        pagina.GetProperty("links").EnumerateArray().Select(l => l.GetProperty("rel").GetString())
            .Should().Contain(["self", "first", "next", "last", "create-comentario"]).And.NotContain("previous");

        // Links de cada item respeitam a autoria: o autor recebe update e delete
        pagina.GetProperty("itens")[0].GetProperty("links").EnumerateArray().Select(l => l.GetProperty("rel").GetString())
            .Should().Contain(["update", "delete"]);
    }

    [Fact]
    public async Task GetComentarios_TamanhoPaginaInvalido_DeveRetornar400()
    {
        // Arrange
        var cliente = _factory.CriarCliente("leitor-400", Papeis.Cidadao);

        // Act
        var resposta = await cliente.GetAsync("/ocorrencias/3008/comentarios?tamanhoPagina=500");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostEtiqueta_ComoCidadao_DeveRetornar403()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-etiqueta", Papeis.Cidadao);

        // Act
        var resposta = await cliente.PostAsJsonAsync("/etiquetas", new { nome = "Urgente" });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CrudEtiqueta_ComoGestor_DeveCriarAtualizarRejeitarDuplicadaERemover()
    {
        // Arrange
        var gestor = _factory.CriarCliente("gestor-etiqueta", Papeis.Gestor);

        // Act + Assert: Create
        var criada = await gestor.PostAsJsonAsync("/etiquetas", new { nome = "Iluminação precária", descricao = "Rua escura à noite" });
        criada.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await criada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();

        // Duplicada (nome igual, sem diferenciar maiúsculas) → 409
        (await gestor.PostAsJsonAsync("/etiquetas", new { nome = "ILUMINAÇÃO PRECÁRIA" })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Update
        var atualizada = await gestor.PutAsJsonAsync($"/etiquetas/{id}", new { nome = "Iluminação ruim", descricao = "Atualizada" });
        atualizada.StatusCode.Should().Be(HttpStatusCode.OK);

        // Read (lista contém as etiquetas iniciais + a nova) e links de gestor
        var lista = await gestor.GetFromJsonAsync<JsonElement>("/etiquetas");
        lista.GetProperty("data").EnumerateArray().Select(e => e.GetProperty("data").GetProperty("nome").GetString())
            .Should().Contain(["Iluminação ruim", "Risco a pedestres"]);
        lista.GetProperty("links").EnumerateArray().Select(l => l.GetProperty("rel").GetString()).Should().Contain("create-etiqueta");

        // Delete
        (await gestor.DeleteAsync($"/etiquetas/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await gestor.GetAsync($"/etiquetas/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAtividades_AposApoioEComentario_DeveListarLinhaDoTempoMaisRecentePrimeiro()
    {
        // Arrange
        var cliente = _factory.CriarCliente("cidadao-timeline", Papeis.Cidadao);
        await cliente.PostIdempotenteAsync("/apoios", new { ocorrenciaId = 3009 });
        await cliente.PostIdempotenteAsync("/comentarios", new { ocorrenciaId = 3009, texto = "Também passo por aqui todo dia" });

        // Act
        var pagina = await cliente.GetFromJsonAsync<JsonElement>("/ocorrencias/3009/atividades");
        var somenteApoios = await cliente.GetFromJsonAsync<JsonElement>("/ocorrencias/3009/atividades?tipo=ApoioRegistrado");

        // Assert
        var tipos = pagina.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("data").GetProperty("tipo").GetString()).ToList();
        tipos.Should().Equal("ComentarioCriado", "ApoioRegistrado");
        somenteApoios.GetProperty("totalRegistros").GetInt64().Should().Be(1);
    }
}
