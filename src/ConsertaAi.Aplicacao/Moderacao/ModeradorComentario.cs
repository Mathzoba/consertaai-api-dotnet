namespace ConsertaAi.Aplicacao.Moderacao;

public sealed class ModeradorComentario : IModeradorComentario
{
    private readonly IEnumerable<IRegraModeracao> _regras;

    public ModeradorComentario(IEnumerable<IRegraModeracao> regras) => _regras = regras;

    public ResultadoModeracao Moderar(string texto)
    {
        var motivos = _regras
            .Select(regra => regra.Avaliar(texto ?? string.Empty))
            .Where(motivo => motivo is not null)
            .Cast<string>()
            .ToList();

        return new ResultadoModeracao(motivos.Count == 0, motivos);
    }
}
