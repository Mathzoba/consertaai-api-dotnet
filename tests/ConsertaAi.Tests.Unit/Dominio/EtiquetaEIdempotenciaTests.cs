using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Excecoes;
using FluentAssertions;

namespace ConsertaAi.Tests.Unit.Dominio;

public class EtiquetaTests
{
    [Fact]
    public void Construtor_DadosValidos_DeveCriarEtiqueta()
    {
        // Act
        var etiqueta = new Etiqueta("  Risco a pedestres ", "  Oferece risco  ");

        // Assert
        etiqueta.Nome.Should().Be("Risco a pedestres");
        etiqueta.Descricao.Should().Be("Oferece risco");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    [InlineData("Um nome de etiqueta comprido demais para o limite")]
    public void Construtor_NomeInvalido_DeveLancarExcecaoDominio(string nomeInvalido)
    {
        // Act
        Action acao = () => new Etiqueta(nomeInvalido, null);

        // Assert
        acao.Should().Throw<ExcecaoDominio>().WithMessage("*nome da etiqueta*");
    }

    [Fact]
    public void Atualizar_DescricaoVazia_DeveGravarNulo()
    {
        // Arrange
        var etiqueta = new Etiqueta("Piorou", "Descrição antiga");

        // Act
        etiqueta.Atualizar("Piorou muito", "   ");

        // Assert
        etiqueta.Nome.Should().Be("Piorou muito");
        etiqueta.Descricao.Should().BeNull();
    }
}

public class RegistroIdempotenciaTests
{
    [Fact]
    public void Construtor_ChaveValida_DeveIniciarEmProcessamento()
    {
        // Act
        var registro = new RegistroIdempotencia("chave-1", "10", "POST /apoios", "HASH");

        // Assert
        registro.EmProcessamento.Should().BeTrue();
        registro.StatusResposta.Should().Be(RegistroIdempotencia.StatusEmProcessamento);
    }

    [Theory]
    [InlineData("10", "POST /apoios", "HASH", true)]
    [InlineData("11", "POST /apoios", "HASH", false)]
    [InlineData("10", "POST /comentarios", "HASH", false)]
    [InlineData("10", "POST /apoios", "OUTRO", false)]
    public void CorrespondeA_Cenarios_DeveExigirMesmoUsuarioEndpointECorpo(string usuario, string endpoint, string hash, bool esperado)
    {
        // Arrange
        var registro = new RegistroIdempotencia("chave-1", "10", "POST /apoios", "HASH");

        // Act & Assert
        registro.CorrespondeA(usuario, endpoint, hash).Should().Be(esperado);
    }

    [Fact]
    public void Concluir_StatusValido_DeveSairDeEmProcessamento()
    {
        // Arrange
        var registro = new RegistroIdempotencia("chave-1", "10", "POST /apoios", "HASH");

        // Act
        registro.Concluir(201, "{\"ok\":true}", "application/json", "/ocorrencias/1/apoios");

        // Assert
        registro.EmProcessamento.Should().BeFalse();
        registro.StatusResposta.Should().Be(201);
        registro.Localizacao.Should().Be("/ocorrencias/1/apoios");
    }

    [Fact]
    public void Expirou_DataAlemDaValidade_DeveRetornarVerdadeiro()
    {
        // Arrange
        var registro = new RegistroIdempotencia("chave-1", "10", "POST /apoios", "HASH");

        // Act
        var expirou = registro.Expirou(TimeSpan.FromHours(24), DateTime.UtcNow.AddHours(25));

        // Assert
        expirou.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Construtor_ChaveVazia_DeveLancarExcecaoDominio(string chave)
    {
        // Act
        Action acao = () => new RegistroIdempotencia(chave, "10", "POST /apoios", "HASH");

        // Assert
        acao.Should().Throw<ExcecaoDominio>();
    }
}

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 1, 10, 0, false, false)]
    [InlineData(25, 1, 10, 3, false, true)]
    [InlineData(25, 2, 10, 3, true, true)]
    [InlineData(25, 3, 10, 3, true, false)]
    public void Metadados_Cenarios_DevemCalcularPaginacao(long total, int pagina, int tamanho, int totalPaginas, bool anterior, bool proxima)
    {
        // Act
        var resultado = new PagedResult<int>([], total, pagina, tamanho);

        // Assert
        resultado.TotalPaginas.Should().Be(totalPaginas);
        resultado.HasPreviousPage.Should().Be(anterior);
        resultado.HasNextPage.Should().Be(proxima);
    }
}
