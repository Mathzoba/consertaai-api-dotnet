using ConsertaAi.Dominio.Excecoes;

namespace ConsertaAi.Dominio.Entidades;

/// <summary>
/// Registro da tabela IDEMPOTENCY_KEY. Guarda a resposta de um POST para devolvê-la igual
/// quando a mesma requisição (mesmo header Idempotency-Key) for repetida, evitando
/// operações duplicadas (ex.: dois apoios por um duplo clique ou por retry de rede do app).
/// </summary>
public class RegistroIdempotencia
{
    public const int TamanhoMaximoChave = 100;

    /// <summary>Status usado enquanto a requisição original ainda está sendo processada.</summary>
    public const int StatusEmProcessamento = 0;

    public string Chave { get; private set; } = string.Empty;
    public string UsuarioId { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string HashCorpo { get; private set; } = string.Empty;
    public int StatusResposta { get; private set; }
    public string? CorpoResposta { get; private set; }
    public string? TipoConteudo { get; private set; }
    public string? Localizacao { get; private set; }
    public DateTime DataCriacao { get; private set; }

    public bool EmProcessamento => StatusResposta == StatusEmProcessamento;

    protected RegistroIdempotencia() { }

    public RegistroIdempotencia(string chave, string usuarioId, string endpoint, string hashCorpo)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > TamanhoMaximoChave)
            throw new ExcecaoDominio($"A chave de idempotência é obrigatória e deve ter até {TamanhoMaximoChave} caracteres.");

        Chave = chave;
        UsuarioId = usuarioId;
        Endpoint = endpoint;
        HashCorpo = hashCorpo;
        StatusResposta = StatusEmProcessamento;
        DataCriacao = DateTime.UtcNow;
    }

    /// <summary>A mesma chave só pode ser reaproveitada pelo mesmo usuário, no mesmo endpoint e com o mesmo corpo.</summary>
    public bool CorrespondeA(string usuarioId, string endpoint, string hashCorpo) =>
        UsuarioId == usuarioId && Endpoint == endpoint && HashCorpo == hashCorpo;

    public bool Expirou(TimeSpan validade, DateTime agoraUtc) => DataCriacao.Add(validade) < agoraUtc;

    /// <summary>Grava a resposta final da requisição original.</summary>
    public void Concluir(int statusResposta, string? corpoResposta, string? tipoConteudo, string? localizacao)
    {
        if (statusResposta < 100)
            throw new ExcecaoDominio("Status HTTP inválido para registro de idempotência.");

        StatusResposta = statusResposta;
        CorpoResposta = corpoResposta;
        TipoConteudo = tipoConteudo;
        Localizacao = localizacao;
    }
}
