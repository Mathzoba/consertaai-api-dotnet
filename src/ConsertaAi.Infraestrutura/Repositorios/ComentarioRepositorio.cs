using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Interfaces;
using ConsertaAi.Infraestrutura.Dados;
using Microsoft.EntityFrameworkCore;

namespace ConsertaAi.Infraestrutura.Repositorios;

/// <summary>
/// Repositório EF Core da tabela COMENTARIO. As etiquetas (N:N) são carregadas
/// explicitamente com Include (tabela de junção) + ThenInclude (etiqueta).
/// </summary>
public class ComentarioRepositorio : IComentarioRepositorio
{
    private readonly AppDbContext _contexto;

    public ComentarioRepositorio(AppDbContext contexto) => _contexto = contexto;

    private IQueryable<Comentario> ComEtiquetas() =>
        _contexto.Comentarios
            .Include(c => c.ComentariosEtiquetas)
                .ThenInclude(ce => ce.Etiqueta);

    public Task<Comentario?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        ComEtiquetas().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<PagedResult<Comentario>> ListarPorOcorrenciaAsync(long ocorrenciaId, int pagina, int tamanhoPagina, bool ascendente, CancellationToken ct = default)
    {
        var consulta = _contexto.Comentarios.AsNoTracking().Where(c => c.OcorrenciaId == ocorrenciaId);

        var total = await consulta.LongCountAsync(ct);

        var ordenada = ascendente
            ? consulta.OrderBy(c => c.DataCriacao).ThenBy(c => c.Id)
            : consulta.OrderByDescending(c => c.DataCriacao).ThenBy(c => c.Id);

        // Paginação executada no banco (OFFSET/FETCH)
        var itens = await ordenada
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Include(c => c.ComentariosEtiquetas)
                .ThenInclude(ce => ce.Etiqueta)
            .AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<Comentario>(itens, total, pagina, tamanhoPagina);
    }

    public async Task AdicionarAsync(Comentario comentario, CancellationToken ct = default)
    {
        _contexto.Comentarios.Add(comentario);
        await _contexto.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Comentario comentario, CancellationToken ct = default)
    {
        // A entidade já está rastreada (carregada por ObterPorIdAsync): basta salvar
        if (_contexto.Entry(comentario).State == EntityState.Detached)
            _contexto.Comentarios.Update(comentario);

        await _contexto.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Comentario comentario, CancellationToken ct = default)
    {
        _contexto.Comentarios.Remove(comentario);
        await _contexto.SaveChangesAsync(ct);
    }
}
