using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Interfaces;
using ConsertaAi.Infraestrutura.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConsertaAi.Infraestrutura.Repositorios;

/// <summary>Repositório EF Core da tabela APOIO.</summary>
public class ApoioRepositorio : IApoioRepositorio
{
    private readonly AppDbContext _contexto;
    private readonly ILogger<ApoioRepositorio> _logger;

    public ApoioRepositorio(AppDbContext contexto, ILogger<ApoioRepositorio> logger)
    {
        _contexto = contexto;
        _logger = logger;
    }

    public Task<Apoio?> ObterPorOcorrenciaEUsuarioAsync(long ocorrenciaId, string usuarioId, CancellationToken ct = default) =>
        _contexto.Apoios.FirstOrDefaultAsync(a => a.OcorrenciaId == ocorrenciaId && a.UsuarioId == usuarioId, ct);

    public Task<bool> ExisteAsync(long ocorrenciaId, string usuarioId, CancellationToken ct = default) =>
        _contexto.Apoios.AsNoTracking().AnyAsync(a => a.OcorrenciaId == ocorrenciaId && a.UsuarioId == usuarioId, ct);

    public Task<int> ContarPorOcorrenciaAsync(long ocorrenciaId, CancellationToken ct = default) =>
        _contexto.Apoios.AsNoTracking().CountAsync(a => a.OcorrenciaId == ocorrenciaId, ct);

    public async Task<bool> AdicionarAsync(Apoio apoio, CancellationToken ct = default)
    {
        _contexto.Apoios.Add(apoio);
        try
        {
            await _contexto.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex)
        {
            // Violação do índice único UX_APOIO_OCORRENCIA_USUARIO (duas requisições simultâneas)
            _contexto.Entry(apoio).State = EntityState.Detached;
            _logger.LogWarning(ex, "Índice único de apoio violado para a ocorrência {OcorrenciaId}", apoio.OcorrenciaId);
            return false;
        }
    }

    public async Task RemoverAsync(Apoio apoio, CancellationToken ct = default)
    {
        _contexto.Apoios.Remove(apoio);
        await _contexto.SaveChangesAsync(ct);
    }
}
