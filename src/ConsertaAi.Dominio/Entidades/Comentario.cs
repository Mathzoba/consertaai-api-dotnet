using ConsertaAi.Dominio.Excecoes;

namespace ConsertaAi.Dominio.Entidades;

/// <summary>
/// Comentário de um usuário em uma ocorrência de zeladoria.
/// Regras: texto obrigatório (3 a 1000 caracteres); somente o autor edita;
/// o autor ou um GESTOR podem remover.
/// </summary>
public class Comentario
{
    public const int TamanhoMinimoTexto = 3;
    public const int TamanhoMaximoTexto = 1000;

    public Guid Id { get; private set; }
    public long OcorrenciaId { get; private set; }
    public string UsuarioId { get; private set; } = string.Empty;
    public string Texto { get; private set; } = string.Empty;
    public DateTime DataCriacao { get; private set; }
    public DateTime? DataEdicao { get; private set; }

    /// <summary>Navegação N:N (lado Comentário → tabela de junção). Carregada com Include/ThenInclude.</summary>
    public ICollection<ComentarioEtiqueta> ComentariosEtiquetas { get; private set; } = new List<ComentarioEtiqueta>();

    /// <summary>Atalho de leitura para as etiquetas associadas.</summary>
    public IEnumerable<Etiqueta> Etiquetas => ComentariosEtiquetas.Select(ce => ce.Etiqueta);

    public bool Editado => DataEdicao.HasValue;

    protected Comentario() { }

    public Comentario(long ocorrenciaId, string usuarioId, string texto)
    {
        if (ocorrenciaId <= 0)
            throw new ExcecaoDominio("O id da ocorrência deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(usuarioId))
            throw new ExcecaoDominio("O autor do comentário é obrigatório.");

        Id = Guid.NewGuid();
        OcorrenciaId = ocorrenciaId;
        UsuarioId = usuarioId.Trim();
        Texto = ValidarTexto(texto);
        DataCriacao = DateTime.UtcNow;
    }

    /// <summary>Indica se o usuário informado é o autor do comentário.</summary>
    public bool EhAutor(string usuarioId) =>
        string.Equals(UsuarioId, usuarioId, StringComparison.Ordinal);

    /// <summary>O autor pode editar; ninguém mais (nem o gestor) altera o texto de outra pessoa.</summary>
    public bool PodeSerEditadoPor(string usuarioId) => EhAutor(usuarioId);

    /// <summary>O autor ou um gestor (moderação) podem remover o comentário.</summary>
    public bool PodeSerRemovidoPor(string usuarioId, bool ehGestor) => ehGestor || EhAutor(usuarioId);

    /// <summary>Edita o texto. Lança <see cref="OperacaoNaoPermitidaException"/> se não for o autor.</summary>
    public void Editar(string usuarioId, string novoTexto)
    {
        if (!PodeSerEditadoPor(usuarioId))
            throw new OperacaoNaoPermitidaException("Somente o autor pode editar o comentário.");

        Texto = ValidarTexto(novoTexto);
        DataEdicao = DateTime.UtcNow;
    }

    /// <summary>Substitui o conjunto de etiquetas do comentário (relação N:N).</summary>
    public void DefinirEtiquetas(IEnumerable<Etiqueta> etiquetas)
    {
        var novas = etiquetas.DistinctBy(e => e.Id).ToList();

        foreach (var existente in ComentariosEtiquetas.Where(ce => novas.All(n => n.Id != ce.EtiquetaId)).ToList())
            ComentariosEtiquetas.Remove(existente);

        foreach (var etiqueta in novas.Where(n => ComentariosEtiquetas.All(ce => ce.EtiquetaId != n.Id)))
            ComentariosEtiquetas.Add(new ComentarioEtiqueta(this, etiqueta));
    }

    private static string ValidarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ExcecaoDominio("O texto do comentário é obrigatório.");

        var normalizado = texto.Trim();
        if (normalizado.Length < TamanhoMinimoTexto || normalizado.Length > TamanhoMaximoTexto)
            throw new ExcecaoDominio($"O texto do comentário deve ter entre {TamanhoMinimoTexto} e {TamanhoMaximoTexto} caracteres.");

        return normalizado;
    }
}
