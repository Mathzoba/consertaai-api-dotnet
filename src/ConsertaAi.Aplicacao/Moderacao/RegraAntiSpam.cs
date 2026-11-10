using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ConsertaAi.Aplicacao.Moderacao;

/// <summary>Estratégia 3: anti-spam (excesso de links ou caracteres repetidos em sequência).</summary>
public sealed partial class RegraAntiSpam : IRegraModeracao
{
    private readonly int _maximoLinks;

    public RegraAntiSpam(IOptions<ModeracaoOptions> opcoes) => _maximoLinks = opcoes.Value.MaximoLinks;

    public string Nome => "anti-spam";

    public string? Avaliar(string texto)
    {
        if (RegexLink().Matches(texto).Count > _maximoLinks)
            return $"O texto pode conter no máximo {_maximoLinks} link(s).";

        if (RegexRepeticao().IsMatch(texto))
            return "O texto contém caracteres repetidos em excesso.";

        return null;
    }

    [GeneratedRegex(@"(https?://|www\.)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexLink();

    [GeneratedRegex(@"(.)\1{7,}")]
    private static partial Regex RegexRepeticao();
}
