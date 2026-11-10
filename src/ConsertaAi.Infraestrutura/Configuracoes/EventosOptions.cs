namespace ConsertaAi.Infraestrutura.Configuracoes;

/// <summary>Seção "Eventos".</summary>
public sealed class EventosOptions
{
    public const string Secao = "Eventos";
    public ModoPublicacaoEventos Modo { get; set; } = ModoPublicacaoEventos.Log;
}
