namespace ConsertaAi.Dominio.Excecoes;

/// <summary>
/// Exceção lançada quando um usuário tenta executar uma operação que o domínio não permite
/// para ele (ex.: editar um comentário de outra pessoa). Convertida em HTTP 403.
/// </summary>
public class OperacaoNaoPermitidaException : Exception
{
    public OperacaoNaoPermitidaException(string mensagem) : base(mensagem) { }
}
