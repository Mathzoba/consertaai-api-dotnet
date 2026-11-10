namespace ConsertaAi.Dominio.Excecoes;

/// <summary>
/// Exceção lançada quando uma regra de negócio do domínio é violada
/// (ex.: texto de comentário vazio, id de ocorrência inválido).
/// É convertida em HTTP 400 pelo tratador global de exceções da API.
/// </summary>
public class ExcecaoDominio : Exception
{
    public ExcecaoDominio(string mensagem) : base(mensagem) { }
}
