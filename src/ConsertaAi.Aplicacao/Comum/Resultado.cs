namespace ConsertaAi.Aplicacao.Comum;

/// <summary>Categorias de erro de aplicação. Cada uma é convertida em um código HTTP pela camada de API.</summary>
public enum TipoErro
{
    /// <summary>Dados de entrada inválidos → 400.</summary>
    Validacao,
    /// <summary>Recurso inexistente → 404.</summary>
    NaoEncontrado,
    /// <summary>Estado conflitante (ex.: apoio duplicado) → 409.</summary>
    Conflito,
    /// <summary>Usuário autenticado sem permissão sobre o recurso (ex.: não é o autor) → 403.</summary>
    Proibido,
    /// <summary>Requisição bem formada, mas que viola uma regra de negócio → 422.</summary>
    RegraNegocio
}

/// <summary>Erro de aplicação com mensagem amigável e, opcionalmente, erros por campo.</summary>
public sealed record Erro(
    TipoErro Tipo,
    string Mensagem,
    IReadOnlyDictionary<string, string[]>? Campos = null)
{
    public static Erro NaoEncontrado(string mensagem) => new(TipoErro.NaoEncontrado, mensagem);
    public static Erro Conflito(string mensagem) => new(TipoErro.Conflito, mensagem);
    public static Erro Proibido(string mensagem) => new(TipoErro.Proibido, mensagem);
    public static Erro RegraNegocio(string mensagem) => new(TipoErro.RegraNegocio, mensagem);

    public static Erro Validacao(string campo, params string[] mensagens) =>
        new(TipoErro.Validacao, "Um ou mais campos são inválidos.",
            new Dictionary<string, string[]> { [campo] = mensagens });
}

/// <summary>
/// Resultado de um caso de uso sem valor de retorno. Evita usar exceções para fluxo de negócio
/// e evita cadeias de if/else nos endpoints (a conversão para HTTP é centralizada).
/// </summary>
public class Resultado
{
    public bool Sucesso => Erro is null;
    public Erro? Erro { get; }

    protected Resultado(Erro? erro) => Erro = erro;

    public static Resultado Ok() => new(null);
    public static Resultado Falha(Erro erro) => new(erro);

    public static implicit operator Resultado(Erro erro) => Falha(erro);
}

/// <summary>Resultado de um caso de uso que devolve um valor em caso de sucesso.</summary>
public sealed class Resultado<T> : Resultado
{
    public T? Valor { get; }

    private Resultado(T? valor, Erro? erro) : base(erro) => Valor = valor;

    public static Resultado<T> Ok(T valor) => new(valor, null);
    public static new Resultado<T> Falha(Erro erro) => new(default, erro);

    public static implicit operator Resultado<T>(T valor) => Ok(valor);
    public static implicit operator Resultado<T>(Erro erro) => Falha(erro);
}
