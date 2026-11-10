namespace ConsertaAi.Dominio.Entidades;

/// <summary>
/// Tabela intermediária (junção) da relação N:N entre <see cref="Comentario"/> e <see cref="Etiqueta"/>.
/// A chave primária é composta (ComentarioId + EtiquetaId), o que impede relacionamentos duplicados
/// — mesmo padrão da aula "Relação N:N com Oracle" (JogoPlataforma).
/// </summary>
public class ComentarioEtiqueta
{
    public Guid ComentarioId { get; private set; }
    public Comentario Comentario { get; private set; } = null!;

    public Guid EtiquetaId { get; private set; }
    public Etiqueta Etiqueta { get; private set; } = null!;

    protected ComentarioEtiqueta() { }

    public ComentarioEtiqueta(Comentario comentario, Etiqueta etiqueta)
    {
        Comentario = comentario;
        ComentarioId = comentario.Id;
        Etiqueta = etiqueta;
        EtiquetaId = etiqueta.Id;
    }
}
