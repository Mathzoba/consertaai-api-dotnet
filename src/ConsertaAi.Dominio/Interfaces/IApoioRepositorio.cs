using ConsertaAi.Dominio.Entidades;

namespace ConsertaAi.Dominio.Interfaces;

/// <summary>Contrato de persistência dos apoios (implementado com EF Core na Infraestrutura).</summary>
public interface IApoioRepositorio
{
    Task<Apoio?> ObterPorOcorrenciaEUsuarioAsync(long ocorrenciaId, string usuarioId, CancellationToken ct = default);
    Task<bool> ExisteAsync(long ocorrenciaId, string usuarioId, CancellationToken ct = default);
    Task<int> ContarPorOcorrenciaAsync(long ocorrenciaId, CancellationToken ct = default);

    /// <summary>Adiciona o apoio. Retorna false se o índice único (ocorrência, usuário) for violado.</summary>
    Task<bool> AdicionarAsync(Apoio apoio, CancellationToken ct = default);

    Task RemoverAsync(Apoio apoio, CancellationToken ct = default);
}
