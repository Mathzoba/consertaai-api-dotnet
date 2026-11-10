using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Aplicacao.Seguranca;
using ConsertaAi.Aplicacao.Servicos;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConsertaAi.Tests.Unit.Aplicacao;

/// <summary>Testes do serviço de apoio com dependências isoladas por Mock (Moq).</summary>
public class ApoioServicoTests
{
    private static readonly UsuarioAutenticado Cidadao = new("10", "Maria", [Papeis.Cidadao]);

    private readonly Mock<IApoioRepositorio> _repositorioMock = new();
    private readonly Mock<IConsultaOcorrencia> _consultaMock = new();
    private readonly Mock<IPublicadorEventos> _publicadorMock = new();
    private readonly Mock<IAtividadeServico> _atividadesMock = new();
    private readonly ApoioServico _servico;

    public ApoioServicoTests()
    {
        _consultaMock.Setup(c => c.ConsultarAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsultaOcorrenciaResultado.NaoVerificada);

        _servico = new ApoioServico(
            _repositorioMock.Object,
            _consultaMock.Object,
            _publicadorMock.Object,
            _atividadesMock.Object,
            NullLogger<ApoioServico>.Instance);
    }

    [Fact]
    public async Task RegistrarAsync_ApoioNovo_DeveSalvarPublicarEventoERetornarTotal()
    {
        // Arrange
        _repositorioMock.Setup(r => r.ExisteAsync(42, "10", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repositorioMock.Setup(r => r.AdicionarAsync(It.IsAny<Apoio>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repositorioMock.Setup(r => r.ContarPorOcorrenciaAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        // Act
        var resultado = await _servico.RegistrarAsync(new RegistrarApoioRequest(42), Cidadao);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor!.TotalApoios.Should().Be(3);
        resultado.Valor.UsuarioId.Should().Be("10");

        _repositorioMock.Verify(r => r.AdicionarAsync(It.Is<Apoio>(a => a.OcorrenciaId == 42 && a.UsuarioId == "10"), It.IsAny<CancellationToken>()), Times.Once);
        _publicadorMock.Verify(p => p.PublicarAsync(
            It.Is<ApoioAlteradoEvento>(e => e.Tipo == ApoioAlteradoEvento.RoutingKeyRegistrado && e.OcorrenciaId == 42 && e.TotalApoios == 3),
            It.IsAny<CancellationToken>()), Times.Once);
        _atividadesMock.Verify(a => a.RegistrarAsync(42, TipoAtividade.ApoioRegistrado, "10", It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarAsync_UsuarioJaApoiou_DeveRetornarConflitoSemSalvar()
    {
        // Arrange
        _repositorioMock.Setup(r => r.ExisteAsync(42, "10", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var resultado = await _servico.RegistrarAsync(new RegistrarApoioRequest(42), Cidadao);

        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro!.Tipo.Should().Be(TipoErro.Conflito);
        _repositorioMock.Verify(r => r.AdicionarAsync(It.IsAny<Apoio>(), It.IsAny<CancellationToken>()), Times.Never);
        _publicadorMock.Verify(p => p.PublicarAsync(It.IsAny<ApoioAlteradoEvento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAsync_IndiceUnicoViolado_DeveRetornarConflitoSemPublicar()
    {
        // Arrange: duas requisições simultâneas — a verificação passa, mas o banco recusa a segunda
        _repositorioMock.Setup(r => r.ExisteAsync(42, "10", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repositorioMock.Setup(r => r.AdicionarAsync(It.IsAny<Apoio>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var resultado = await _servico.RegistrarAsync(new RegistrarApoioRequest(42), Cidadao);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Conflito);
        _publicadorMock.Verify(p => p.PublicarAsync(It.IsAny<ApoioAlteradoEvento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAsync_OcorrenciaInexistenteNaApiJava_DeveRetornarNaoEncontrado()
    {
        // Arrange
        _consultaMock.Setup(c => c.ConsultarAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsultaOcorrenciaResultado.NaoEncontrada);

        // Act
        var resultado = await _servico.RegistrarAsync(new RegistrarApoioRequest(999), Cidadao);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
        _repositorioMock.Verify(r => r.ExisteAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("RESOLVIDA")]
    [InlineData("CANCELADA")]
    public async Task RegistrarAsync_OcorrenciaEncerrada_DeveRetornarRegraNegocio(string status)
    {
        // Arrange
        _consultaMock.Setup(c => c.ConsultarAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsultaOcorrenciaResultado.Encontrada(status));

        // Act
        var resultado = await _servico.RegistrarAsync(new RegistrarApoioRequest(42), Cidadao);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.RegraNegocio);
        _repositorioMock.Verify(r => r.AdicionarAsync(It.IsAny<Apoio>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetirarAsync_SemApoioPrevio_DeveRetornarNaoEncontrado()
    {
        // Arrange
        _repositorioMock.Setup(r => r.ObterPorOcorrenciaEUsuarioAsync(42, "10", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Apoio?)null);

        // Act
        var resultado = await _servico.RetirarAsync(42, Cidadao);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
        _repositorioMock.Verify(r => r.RemoverAsync(It.IsAny<Apoio>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetirarAsync_ApoioExistente_DeveRemoverEPublicarEventoDeRemocao()
    {
        // Arrange
        var apoio = new Apoio(42, "10");
        _repositorioMock.Setup(r => r.ObterPorOcorrenciaEUsuarioAsync(42, "10", It.IsAny<CancellationToken>())).ReturnsAsync(apoio);
        _repositorioMock.Setup(r => r.ContarPorOcorrenciaAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        // Act
        var resultado = await _servico.RetirarAsync(42, Cidadao);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        _repositorioMock.Verify(r => r.RemoverAsync(apoio, It.IsAny<CancellationToken>()), Times.Once);
        _publicadorMock.Verify(p => p.PublicarAsync(
            It.Is<ApoioAlteradoEvento>(e => e.Tipo == ApoioAlteradoEvento.RoutingKeyRemovido && e.TotalApoios == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterResumoAsync_UsuarioQueApoiou_DeveInformarTotalEApoio()
    {
        // Arrange
        _repositorioMock.Setup(r => r.ContarPorOcorrenciaAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(5);
        _repositorioMock.Setup(r => r.ExisteAsync(42, "10", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var resumo = await _servico.ObterResumoAsync(42, Cidadao);

        // Assert
        resumo.Should().Be(new ResumoApoiosResponse(42, 5, true));
    }
}
