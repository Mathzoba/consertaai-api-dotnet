using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Interfaces;
using ConsertaAi.Infraestrutura.Dados;
using Microsoft.EntityFrameworkCore;

namespace ConsertaAi.Infraestrutura.Repositorios;

/// <summary>Repositório EF Core da tabela ETIQUETA.</summary>
public class EtiquetaRepositorio : IEtiquetaRepositorio
{
    private readonly AppDbContext _contexto;

    public EtiquetaRepositorio(AppDbContext contexto) => _contexto = contexto;

    public async Task<IReadOnlyList<Etiqueta>> ListarAsync(CancellationToken ct = default) =>
        await _contexto.Etiquetas.AsNoTracking().OrderBy(e => e.Nome).ToListAsync(ct);

    public Task<Etiqueta?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Etiquetas.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Etiqueta>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await _contexto.Etiquetas.Where(e => ids.Contains(e.Id)).ToListAsync(ct);

    public Task<bool> ExisteComNomeAsync(string nome, Guid? ignorarId = null, CancellationToken ct = default)
    {
        var nomeNormalizado = nome.Trim().ToUpper();
        return _contexto.Etiquetas.AsNoTracking()
            .AnyAsync(e => e.Nome.ToUpper() == nomeNormalizado && (ignorarId == null || e.Id != ignorarId), ct);
    }

    public async Task AdicionarAsync(Etiqueta etiqueta, CancellationToken ct = default)
    {
        _contexto.Etiquetas.Add(etiqueta);
        await _contexto.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Etiqueta etiqueta, CancellationToken ct = default)
    {
        if (_contexto.Entry(etiqueta).State == EntityState.Detached)
            _contexto.Etiquetas.Update(etiqueta);

        await _contexto.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Etiqueta etiqueta, CancellationToken ct = default)
    {
        _contexto.Etiquetas.Remove(etiqueta);
        await _contexto.SaveChangesAsync(ct);
    }
}
