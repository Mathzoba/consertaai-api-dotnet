namespace ConsertaAi.Aplicacao.Moderacao;

/// <summary>Executa todas as estratégias de moderação registradas.</summary>
public interface IModeradorComentario
{
    ResultadoModeracao Moderar(string texto);
}
