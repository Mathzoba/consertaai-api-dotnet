namespace ConsertaAi.Aplicacao.Moderacao;

/// <summary>Opções de moderação (seção "Moderacao" do appsettings).</summary>
public sealed class ModeracaoOptions
{
    public const string Secao = "Moderacao";

    public List<string> PalavrasProibidas { get; set; } =
    [
        "idiota", "imbecil", "otario", "otaria", "babaca", "cretino", "cretina", "vagabundo", "vagabunda", "desgracado", "desgracada"
    ];

    public int MaximoLinks { get; set; } = 1;
}
