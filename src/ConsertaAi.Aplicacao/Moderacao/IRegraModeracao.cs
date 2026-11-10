namespace ConsertaAi.Aplicacao.Moderacao;

/// <summary>
/// Strategy Pattern (OCP + DIP): cada regra de moderação de texto é uma estratégia isolada.
/// Para criar uma regra nova basta implementar esta interface e registrá-la no DI —
/// o moderador não precisa ser alterado (nada de cadeias de if/else).
/// </summary>
public interface IRegraModeracao
{
    string Nome { get; }

    /// <summary>Retorna null se o texto foi aprovado, ou o motivo da reprovação.</summary>
    string? Avaliar(string texto);
}
