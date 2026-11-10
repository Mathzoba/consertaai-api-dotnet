using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Excecoes;
using FluentAssertions;

namespace ConsertaAi.Tests.Unit.Dominio;

/// <summary>Testes das regras puras da entidade Apoio (padrão AAA).</summary>
public class ApoioTests
{
    [Fact]
    public void Construtor_DadosValidos_DeveCriarInstanciaComSucesso()
    {
        // Arrange
        const long ocorrenciaId = 42;
        const string usuarioId = "10";

        // Act
        var apoio = new Apoio(ocorrenciaId, usuarioId);

        // Assert
        apoio.Id.Should().NotBeEmpty();
        apoio.OcorrenciaId.Should().Be(ocorrenciaId);
        apoio.UsuarioId.Should().Be(usuarioId);
        apoio.DataApoio.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Construtor_OcorrenciaInvalida_DeveLancarExcecaoDominio(long ocorrenciaInvalida)
    {
        // Act
        Action acao = () => new Apoio(ocorrenciaInvalida, "10");

        // Assert
        acao.Should().Throw<ExcecaoDominio>().WithMessage("*id da ocorrência deve ser maior que zero*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Construtor_UsuarioInvalido_DeveLancarExcecaoDominio(string? usuarioInvalido)
    {
        // Act
        Action acao = () => new Apoio(1, usuarioInvalido!);

        // Assert
        acao.Should().Throw<ExcecaoDominio>().WithMessage("*usuário do apoio é obrigatório*");
    }

    [Theory]
    [InlineData("10", true)]
    [InlineData("11", false)]
    public void PertenceA_UsuarioInformado_DeveIndicarSeEhODono(string usuario, bool esperado)
    {
        // Arrange
        var apoio = new Apoio(1, "10");

        // Act
        var resultado = apoio.PertenceA(usuario);

        // Assert
        resultado.Should().Be(esperado);
    }
}
