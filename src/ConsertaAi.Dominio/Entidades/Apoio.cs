using ConsertaAi.Dominio.Excecoes;

namespace ConsertaAi.Dominio.Entidades;

/// <summary>
/// Apoio de um cidadão a uma ocorrência de zeladoria já reportada.
/// Em vez de criar um report duplicado, o cidadão "apoia" o existente, aumentando a prioridade dele.
/// Regra: um mesmo usuário só pode apoiar uma ocorrência uma única vez
/// (garantida no serviço e por índice único (ID_OCORRENCIA, ID_USUARIO) no banco).
/// </summary>
public class Apoio
{
    public Guid Id { get; private set; }

    /// <summary>Id da ocorrência na API Java (não há chave estrangeira entre bancos).</summary>
    public long OcorrenciaId { get; private set; }

    /// <summary>Id do usuário vindo da claim "sub" do token JWT.</summary>
    public string UsuarioId { get; private set; } = string.Empty;

    public DateTime DataApoio { get; private set; }

    // Construtor exigido pelo Entity Framework Core
    protected Apoio() { }

    public Apoio(long ocorrenciaId, string usuarioId)
    {
        if (ocorrenciaId <= 0)
            throw new ExcecaoDominio("O id da ocorrência deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(usuarioId))
            throw new ExcecaoDominio("O usuário do apoio é obrigatório.");

        Id = Guid.NewGuid();
        OcorrenciaId = ocorrenciaId;
        UsuarioId = usuarioId.Trim();
        DataApoio = DateTime.UtcNow;
    }

    /// <summary>Indica se o apoio pertence ao usuário informado.</summary>
    public bool PertenceA(string usuarioId) =>
        string.Equals(UsuarioId, usuarioId, StringComparison.Ordinal);
}
