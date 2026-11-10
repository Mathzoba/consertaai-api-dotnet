using ConsertaAi.Dominio.Entidades;

namespace ConsertaAi.Aplicacao.Moderacao;

/// <summary>Estratégia 1: tamanho do texto (mesmos limites da entidade Comentario).</summary>
public sealed class RegraTamanhoTexto : IRegraModeracao
{
    public string Nome => "tamanho";

    public string? Avaliar(string texto)
    {
        var tamanho = texto.Trim().Length;
        return tamanho is < Comentario.TamanhoMinimoTexto or > Comentario.TamanhoMaximoTexto
            ? $"O texto deve ter entre {Comentario.TamanhoMinimoTexto} e {Comentario.TamanhoMaximoTexto} caracteres."
            : null;
    }
}
