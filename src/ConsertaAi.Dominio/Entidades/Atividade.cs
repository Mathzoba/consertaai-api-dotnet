using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Excecoes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ConsertaAi.Dominio.Entidades;

/// <summary>
/// Documento NoSQL (MongoDB, collection "atividades") com a linha do tempo de participação
/// de uma ocorrência: apoios registrados/retirados e comentários criados/editados/removidos.
/// É um modelo de leitura: consultado com filtro, ordenação e paginação executados no MongoDB.
/// </summary>
public class Atividade
{
    // Define esta propriedade como o identificador único (_id) do documento no MongoDB
    [BsonId]
    // Converte o Guid do C# para uma String legível dentro do banco
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; private set; }

    [BsonElement("ocorrenciaId")]
    public long OcorrenciaId { get; private set; }

    [BsonElement("tipo")]
    [BsonRepresentation(BsonType.String)]
    public TipoAtividade Tipo { get; private set; }

    [BsonElement("usuarioId")]
    public string UsuarioId { get; private set; } = string.Empty;

    [BsonElement("descricao")]
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Id do recurso relacionado (apoio ou comentário), quando houver.</summary>
    [BsonElement("recursoId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? RecursoId { get; private set; }

    [BsonElement("dataRegistro")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime DataRegistro { get; private set; }

    // Construtor usado pelo driver do MongoDB na desserialização
    [BsonConstructor]
    private Atividade() { }

    public Atividade(long ocorrenciaId, TipoAtividade tipo, string usuarioId, string descricao, Guid? recursoId = null)
    {
        if (ocorrenciaId <= 0)
            throw new ExcecaoDominio("O id da ocorrência deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(usuarioId))
            throw new ExcecaoDominio("O usuário da atividade é obrigatório.");

        Id = Guid.NewGuid();
        OcorrenciaId = ocorrenciaId;
        Tipo = tipo;
        UsuarioId = usuarioId;
        Descricao = descricao;
        RecursoId = recursoId;
        DataRegistro = DateTime.UtcNow;
    }
}
