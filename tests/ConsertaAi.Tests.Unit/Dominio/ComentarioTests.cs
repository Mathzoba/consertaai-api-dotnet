using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Excecoes;
using FluentAssertions;

namespace ConsertaAi.Tests.Unit.Dominio;

/// <summary>Regras do Comentário: validação de texto, autoria na edição e permissão de remoção.</summary>
public class ComentarioTests
{
    public static TheoryData<string?> TextosInvalidos => new()
    {
        null,
        "",
        "   ",
        "ab",
        new string('x', Comentario.TamanhoMaximoTexto + 1)
    };

    [Fact]
    public void Construtor_DadosValidos_DeveCriarComTextoNormalizado()
    {
        // Arrange
        const string texto = "  Poste apagado há 3 dias  ";

        // Act
        var comentario = new Comentario(7, "10", texto);

        // Assert
        comentario.Id.Should().NotBeEmpty();
        comentario.Texto.Should().Be("Poste apagado há 3 dias");
        comentario.Editado.Should().BeFalse();
        comentario.DataEdicao.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(TextosInvalidos))]
    public void Construtor_TextoInvalido_DeveLancarExcecaoDominio(string? textoInvalido)
    {
        // Act
        Action acao = () => new Comentario(7, "10", textoInvalido!);

        // Assert
        acao.Should().Throw<ExcecaoDominio>().WithMessage("*texto do comentário*");
    }

    [Fact]
    public void Editar_PeloAutor_DeveAlterarTextoEMarcarComoEditado()
    {
        // Arrange
        var comentario = new Comentario(7, "10", "Texto original");

        // Act
        comentario.Editar("10", "Texto corrigido pelo autor");

        // Assert
        comentario.Texto.Should().Be("Texto corrigido pelo autor");
        comentario.Editado.Should().BeTrue();
        comentario.DataEdicao.Should().NotBeNull();
    }

    [Fact]
    public void Editar_PorOutroUsuario_DeveLancarOperacaoNaoPermitida()
    {
        // Arrange
        var comentario = new Comentario(7, "10", "Texto original");

        // Act
        Action acao = () => comentario.Editar("99", "Tentativa de edição alheia");

        // Assert
        acao.Should().Throw<OperacaoNaoPermitidaException>().WithMessage("*Somente o autor*");
        comentario.Texto.Should().Be("Texto original");
    }

    [Theory]
    [InlineData("10", false, true)]  // autor
    [InlineData("99", true, true)]   // gestor (moderação)
    [InlineData("99", false, false)] // outro cidadão
    public void PodeSerRemovidoPor_Cenarios_DeveRespeitarAutorOuGestor(string usuario, bool ehGestor, bool esperado)
    {
        // Arrange
        var comentario = new Comentario(7, "10", "Comentário qualquer");

        // Act
        var resultado = comentario.PodeSerRemovidoPor(usuario, ehGestor);

        // Assert
        resultado.Should().Be(esperado);
    }

    [Fact]
    public void DefinirEtiquetas_ListaComDuplicadas_DeveManterApenasEtiquetasDistintas()
    {
        // Arrange
        var comentario = new Comentario(7, "10", "Comentário com etiquetas");
        var risco = new Etiqueta("Risco a pedestres", null);
        var recorrente = new Etiqueta("Problema recorrente", null);

        // Act
        comentario.DefinirEtiquetas([risco, recorrente, risco]);

        // Assert
        comentario.Etiquetas.Should().HaveCount(2).And.Contain([risco, recorrente]);
    }

    [Fact]
    public void DefinirEtiquetas_NovaLista_DeveSubstituirAsAnteriores()
    {
        // Arrange
        var comentario = new Comentario(7, "10", "Comentário com etiquetas");
        var risco = new Etiqueta("Risco a pedestres", null);
        var piorou = new Etiqueta("Piorou", null);
        comentario.DefinirEtiquetas([risco]);

        // Act
        comentario.DefinirEtiquetas([piorou]);

        // Assert
        comentario.Etiquetas.Should().ContainSingle().Which.Should().Be(piorou);
    }
}
