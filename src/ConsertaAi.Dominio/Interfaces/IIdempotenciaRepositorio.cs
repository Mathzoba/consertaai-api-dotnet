using ConsertaAi.Dominio.Entidades;

namespace ConsertaAi.Dominio.Interfaces;

/// <summary>Contrato de persistência da tabela IDEMPOTENCY_KEY.</summary>
public interface IIdempotenciaRepositorio
{
    Task<RegistroIdempotencia?> ObterAsync(string chave, CancellationToken ct = default);

    /// <summary>
    /// Tenta gravar o registro "em processamento". Retorna false se outra requisição
    /// com a mesma chave já reservou o registro (corrida entre requisições simultâneas).
    /// </summary>
    Task<bool> TentarReservarAsync(RegistroIdempotencia registro, CancellationToken ct = default);

    Task AtualizarAsync(RegistroIdempotencia registro, CancellationToken ct = default);
    Task RemoverAsync(RegistroIdempotencia registro, CancellationToken ct = default);
}
