using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Aplicacao.Moderacao;
using ConsertaAi.Aplicacao.Seguranca;
using ConsertaAi.Aplicacao.Servicos;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaAi.Tests.Unit.Aplicacao;

/// <summary>Testes do serviço de comentários: moderação, etiquetas N:N e regra de autoria.</summary>
public class ComentarioServicoTests
{
    private static readonly UsuarioAutenticado Autor = new("10", "Maria", [Papeis.Cidadao]);
    private static readonly UsuarioAutenticado OutroCidadao = new("20", "João", [Papeis.Cidadao]);
    private static readonly UsuarioAutenticado Gestor = new("30", "Ana", [Papeis.Gestor]);

    private readonly Mock<IComentarioRepositorio> _comentariosMock = new();
    private readonly Mock<IEtiquetaRepositorio> _etiquetasMock = new();
    private readonly Mock<IConsultaOcorrencia> _consultaMock = new();
    private readonly Mock<IAtividadeServico> _atividadesMock = new();
    private readonly ComentarioServico _servico;

    public ComentarioServicoTests()
    {
        var opcoes = Options.Create(new ModeracaoOptions());
        var moderador = new ModeradorComentario([new RegraTamanhoTexto(), new RegraLinguagemOfensiva(opcoes), new RegraAntiSpam(opcoes)]);

        _consultaMock.Setup(c => c.ConsultarAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsultaOcorrenciaResultado.NaoVerificada);

        _servico = new ComentarioServico(
            _comentariosMock.Object, _etiquetasMock.Object, moderador, _consultaMock.Object, _atividadesMock.Object,
            NullLogger<ComentarioServico>.Instance);
    }

    [Fact]
    public async Task CriarAsync_TextoValidoComEtiqueta_DeveSalvarComRelacaoNN()
    {
        // Arrange
        var etiqueta = new Etiqueta("Risco a pedestres", null);
        _etiquetasMock.Setup(e => e.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([etiqueta]);
        var request = new CriarComentarioRequest(7, "O buraco aumentou depois da chuva", [etiqueta.Id]);

        // Act
        var resultado = await _servico.CriarAsync(request, Autor);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor!.Etiquetas.Should().ContainSingle(e => e.Nome == "Risco a pedestres");
        _comentariosMock.Verify(r => r.AdicionarAsync(It.Is<Comentario>(c => c.UsuarioId == "10" && c.OcorrenciaId == 7), It.IsAny<CancellationToken>()), Times.Once);
        _atividadesMock.Verify(a => a.RegistrarAsync(7, TipoAtividade.ComentarioCriado, "10", It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Esse gestor é um IDIOTA")]
    [InlineData("Que serviço de otário")]
    [InlineData("veja http://a.com e http://b.com")]
    [InlineData("socorrooooooooooooo")]
    public async Task CriarAsync_TextoReprovadoNaModeracao_DeveRetornarValidacaoSemSalvar(string texto)
    {
        // Act
        var resultado = await _servico.CriarAsync(new CriarComentarioRequest(7, texto), Autor);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Validacao);
        resultado.Erro.Campos.Should().ContainKey("texto");
        _comentariosMock.Verify(r => r.AdicionarAsync(It.IsAny<Comentario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAsync_EtiquetaInexistente_DeveRetornarValidacao()
    {
        // Arrange
        _etiquetasMock.Setup(e => e.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var resultado = await _servico.CriarAsync(new CriarComentarioRequest(7, "Comentário válido", [Guid.NewGuid()]), Autor);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Validacao);
        resultado.Erro.Campos.Should().ContainKey("etiquetaIds");
    }

    [Fact]
    public async Task CriarAsync_OcorrenciaCancelada_DeveRetornarRegraNegocio()
    {
        // Arrange
        _consultaMock.Setup(c => c.ConsultarAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsultaOcorrenciaResultado.Encontrada("CANCELADA"));

        // Act
        var resultado = await _servico.CriarAsync(new CriarComentarioRequest(7, "Comentário válido"), Autor);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.RegraNegocio);
    }

    [Fact]
    public async Task AtualizarAsync_PorOutroUsuario_DeveRetornarProibidoSemAtualizar()
    {
        // Arrange
        var comentario = new Comentario(7, Autor.Id, "Texto do autor");
        _comentariosMock.Setup(r => r.ObterPorIdAsync(comentario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comentario);

        // Act
        var resultado = await _servico.AtualizarAsync(comentario.Id, new AtualizarComentarioRequest("Texto alterado"), OutroCidadao);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Proibido);
        comentario.Texto.Should().Be("Texto do autor");
        _comentariosMock.Verify(r => r.AtualizarAsync(It.IsAny<Comentario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AtualizarAsync_GestorQueNaoEhAutor_DeveRetornarProibido()
    {
        // Arrange: o gestor modera (remove), mas não reescreve o texto do cidadão
        var comentario = new Comentario(7, Autor.Id, "Texto do autor");
        _comentariosMock.Setup(r => r.ObterPorIdAsync(comentario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comentario);

        // Act
        var resultado = await _servico.AtualizarAsync(comentario.Id, new AtualizarComentarioRequest("Texto alterado"), Gestor);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Proibido);
    }

    [Fact]
    public async Task AtualizarAsync_PeloAutor_DeveAtualizarERegistrarAtividade()
    {
        // Arrange
        var comentario = new Comentario(7, Autor.Id, "Texto do autor");
        _comentariosMock.Setup(r => r.ObterPorIdAsync(comentario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comentario);

        // Act
        var resultado = await _servico.AtualizarAsync(comentario.Id, new AtualizarComentarioRequest("Texto corrigido"), Autor);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor!.Editado.Should().BeTrue();
        _comentariosMock.Verify(r => r.AtualizarAsync(comentario, It.IsAny<CancellationToken>()), Times.Once);
        _atividadesMock.Verify(a => a.RegistrarAsync(7, TipoAtividade.ComentarioEditado, Autor.Id, It.IsAny<string>(), comentario.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoverAsync_PorGestor_DeveRemover()
    {
        // Arrange
        var comentario = new Comentario(7, Autor.Id, "Texto do autor");
        _comentariosMock.Setup(r => r.ObterPorIdAsync(comentario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comentario);

        // Act
        var resultado = await _servico.RemoverAsync(comentario.Id, Gestor);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        _comentariosMock.Verify(r => r.RemoverAsync(comentario, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoverAsync_PorOutroCidadao_DeveRetornarProibido()
    {
        // Arrange
        var comentario = new Comentario(7, Autor.Id, "Texto do autor");
        _comentariosMock.Setup(r => r.ObterPorIdAsync(comentario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comentario);

        // Act
        var resultado = await _servico.RemoverAsync(comentario.Id, OutroCidadao);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Proibido);
        _comentariosMock.Verify(r => r.RemoverAsync(It.IsAny<Comentario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObterPorIdAsync_ComentarioInexistente_DeveRetornarNaoEncontrado()
    {
        // Arrange
        var id = Guid.NewGuid();
        _comentariosMock.Setup(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Comentario?)null);

        // Act
        var resultado = await _servico.ObterPorIdAsync(id);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
        _comentariosMock.Verify(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
