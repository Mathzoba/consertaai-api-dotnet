using ConsertaAi.Dominio.Excecoes;

namespace ConsertaAi.Dominio.Entidades;

/// <summary>
/// Etiqueta (tag) que classifica comentários, ex.: "Risco a pedestres", "Problema recorrente",
/// "Informação nova". Mantida pelo gestor. Relaciona-se N:N com <see cref="Comentario"/>
/// por meio da tabela intermediária <see cref="ComentarioEtiqueta"/>.
/// </summary>
public class Etiqueta
{
    public const int TamanhoMinimoNome = 2;
    public const int TamanhoMaximoNome = 40;
    public const int TamanhoMaximoDescricao = 200;

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public DateTime DataCriacao { get; private set; }

    /// <summary>Navegação N:N (lado Etiqueta → tabela de junção).</summary>
    public ICollection<ComentarioEtiqueta> ComentariosEtiquetas { get; private set; } = new List<ComentarioEtiqueta>();

    protected Etiqueta() { }

    public Etiqueta(string nome, string? descricao)
    {
        Id = Guid.NewGuid();
        DataCriacao = DateTime.UtcNow;
        DefinirDados(nome, descricao);
    }

    public void Atualizar(string nome, string? descricao) => DefinirDados(nome, descricao);

    private void DefinirDados(string nome, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ExcecaoDominio("O nome da etiqueta é obrigatório.");

        var nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Length < TamanhoMinimoNome || nomeNormalizado.Length > TamanhoMaximoNome)
            throw new ExcecaoDominio($"O nome da etiqueta deve ter entre {TamanhoMinimoNome} e {TamanhoMaximoNome} caracteres.");

        var descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada is { Length: > TamanhoMaximoDescricao })
            throw new ExcecaoDominio($"A descrição da etiqueta deve ter no máximo {TamanhoMaximoDescricao} caracteres.");

        Nome = nomeNormalizado;
        Descricao = descricaoNormalizada;
    }
}
