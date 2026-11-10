namespace ConsertaAi.Aplicacao.Moderacao;

/// <summary>Resultado consolidado da moderação.</summary>
public sealed record ResultadoModeracao(bool Aprovado, IReadOnlyList<string> Motivos);
