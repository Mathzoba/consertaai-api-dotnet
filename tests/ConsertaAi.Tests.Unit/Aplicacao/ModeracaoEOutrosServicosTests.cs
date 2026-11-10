using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Aplicacao.Moderacao;
using ConsertaAi.Aplicacao.Servicos;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaAi.Tests.Unit.Aplicacao;

/// <summary>Cada estratégia de moderação é testada isoladamente (Strategy Pattern).</summary>
public class ModeracaoTests
{
    private readonly IOptions<ModeracaoOptions> _opcoes = Options.Create(new ModeracaoOptions());

    [Theory]
    [InlineData("Que IDIOTA fez isso")]
    [InlineData("serviço de otário")]
    [InlineData("Imbecil")]
    public void RegraLinguagemOfensiva_TextoOfensivo_DeveReprovarIgnorandoAcentosEMaiusculas(string texto)
    {
        // Arrange
        var regra = new RegraLinguagemOfensiva(_opcoes);

        // Act
        var motivo = regra.Avaliar(texto);

        // Assert
        motivo.Should().NotBeNull().And.Contain("linguagem ofensiva");
    }

    [Theory]
    [InlineData("O lixo não é recolhido há uma semana")]
    [InlineData("Calçada quebrada em frente à escola")]
    public void RegraLinguagemOfensiva_TextoRespeitoso_DeveAprovar(string texto)
    {
        // Act
        var motivo = new RegraLinguagemOfensiva(_opcoes).Avaliar(texto);

        // Assert
        motivo.Should().BeNull();
    }

    [Theory]
    [InlineData("visite http://a.com e www.b.com", true)]
    [InlineData("Fotos em https://drive.exemplo.com/abc", false)]
    [InlineData("aaaaaaaaaaaaaaa", true)]
    [InlineData("Buraco grande na rua", false)]
    public void RegraAntiSpam_Cenarios_DeveDetectarSpam(string texto, bool deveReprovar)
    {
        // Act
        var motivo = new RegraAntiSpam(_opcoes).Avaliar(texto);

        // Assert
        (motivo is not null).Should().Be(deveReprovar);
    }

    [Fact]
    public void ModeradorComentario_VariasRegrasReprovando_DeveAcumularTodosOsMotivos()
    {
        // Arrange
        var regraA = new Mock<IRegraModeracao>();
        regraA.Setup(r => r.Avaliar(It.IsAny<string>())).Returns("motivo A");
        var regraB = new Mock<IRegraModeracao>();
        regraB.Setup(r => r.Avaliar(It.IsAny<string>())).Returns("motivo B");
        var regraOk = new Mock<IRegraModeracao>();
        regraOk.Setup(r => r.Avaliar(It.IsAny<string>())).Returns((string?)null);
        var moderador = new ModeradorComentario([regraA.Object, regraOk.Object, regraB.Object]);

        // Act
        var resultado = moderador.Moderar("qualquer texto");

        // Assert
        resultado.Aprovado.Should().BeFalse();
        resultado.Motivos.Should().BeEquivalentTo(["motivo A", "motivo B"]);
        regraOk.Verify(r => r.Avaliar("qualquer texto"), Times.Once);
    }
}

public class ConsultaOcorrenciaResultadoTests
{
    [Theory]
    [InlineData("ABERTA", true)]
    [InlineData("EM_ANALISE", true)]
    [InlineData("em_andamento", true)]
    [InlineData("RESOLVIDA", false)]
    [InlineData("CANCELADA", false)]
    public void PermiteApoio_StatusDaOcorrencia_DeveAceitarSomenteNaoEncerradas(string status, bool esperado)
    {
        // Act & Assert
        ConsultaOcorrenciaResultado.Encontrada(status).PermiteApoio.Should().Be(esperado);
    }

    [Fact]
    public void PermiteApoio_ApiJavaIndisponivel_NaoDeveBloquearOCidadao()
    {
        // Act & Assert
        ConsultaOcorrenciaResultado.Indisponivel.PermiteApoio.Should().BeTrue();
    }
}

public class EtiquetaServicoTests
{
    private readonly Mock<IEtiquetaRepositorio> _repositorioMock = new();
    private readonly EtiquetaServico _servico;

    public EtiquetaServicoTests() =>
        _servico = new EtiquetaServico(_repositorioMock.Object, NullLogger<EtiquetaServico>.Instance);

    [Fact]
    public async Task CriarAsync_NomeInedito_DeveSalvar()
    {
        // Arrange
        _repositorioMock.Setup(r => r.ExisteComNomeAsync("Urgente", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var resultado = await _servico.CriarAsync(new SalvarEtiquetaRequest("Urgente", "Precisa de atenção imediata"));

        // Assert
        resultado.Sucesso.Should().BeTrue();
        _repositorioMock.Verify(r => r.AdicionarAsync(It.Is<Etiqueta>(e => e.Nome == "Urgente"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_NomeDuplicado_DeveRetornarConflito()
    {
        // Arrange
        _repositorioMock.Setup(r => r.ExisteComNomeAsync("Urgente", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var resultado = await _servico.CriarAsync(new SalvarEtiquetaRequest("Urgente"));

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Conflito);
        _repositorioMock.Verify(r => r.AdicionarAsync(It.IsAny<Etiqueta>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoverAsync_EtiquetaInexistente_DeveRetornarNaoEncontrado()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositorioMock.Setup(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Etiqueta?)null);

        // Act
        var resultado = await _servico.RemoverAsync(id);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
    }
}

public class AtividadeServicoTests
{
    [Fact]
    public async Task RegistrarAsync_MongoIndisponivel_NaoDeveInterromperOFluxo()
    {
        // Arrange
        var repositorio = new Mock<IAtividadeRepositorio>();
        repositorio.Setup(r => r.AdicionarAsync(It.IsAny<Atividade>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("MongoDB fora do ar"));
        var servico = new AtividadeServico(repositorio.Object, NullLogger<AtividadeServico>.Instance);

        // Act
        var acao = () => servico.RegistrarAsync(1, TipoAtividade.ApoioRegistrado, "10", "Apoio", null);

        // Assert
        await acao.Should().NotThrowAsync();
        repositorio.Verify(r => r.AdicionarAsync(It.IsAny<Atividade>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
