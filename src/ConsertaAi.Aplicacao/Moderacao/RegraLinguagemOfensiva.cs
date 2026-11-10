using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ConsertaAi.Aplicacao.Moderacao;

/// <summary>Estratégia 2: bloqueia linguagem ofensiva (ignora acentos e maiúsculas).</summary>
public sealed class RegraLinguagemOfensiva : IRegraModeracao
{
    private readonly IReadOnlyList<Regex> _padroes;

    public RegraLinguagemOfensiva(IOptions<ModeracaoOptions> opcoes)
    {
        _padroes = opcoes.Value.PalavrasProibidas
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new Regex($@"\b{Regex.Escape(Normalizar(p))}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant))
            .ToList();
    }

    public string Nome => "linguagem-ofensiva";

    public string? Avaliar(string texto)
    {
        var normalizado = Normalizar(texto);
        return _padroes.Any(p => p.IsMatch(normalizado))
            ? "O texto contém linguagem ofensiva. Descreva o problema de forma respeitosa."
            : null;
    }

    internal static string Normalizar(string texto)
    {
        var decomposto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
