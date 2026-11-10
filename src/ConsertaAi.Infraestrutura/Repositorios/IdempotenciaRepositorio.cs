using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Interfaces;
using ConsertaAi.Infraestrutura.Dados;
using Microsoft.EntityFrameworkCore;

namespace ConsertaAi.Infraestrutura.Repositorios;

/// <summary>Repositório EF Core da tabela IDEMPOTENCY_KEY.</summary>
public class IdempotenciaRepositorio : IIdempotenciaRepositorio
{
    private readonly AppDbContext _contexto;

    public IdempotenciaRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<RegistroIdempotencia?> ObterAsync(string chave, CancellationToken ct = default) =>
        _contexto.RegistrosIdempotencia.FirstOrDefaultAsync(r => r.Chave == chave, ct);

    public async Task<bool> TentarReservarAsync(RegistroIdempotencia registro, CancellationToken ct = default)
    {
        _contexto.RegistrosIdempotencia.Add(registro);
        try
        {
            await _contexto.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException)
        {
            // Chave primária já existe: outra requisição com a mesma Idempotency-Key chegou primeiro
            _contexto.Entry(registro).State = EntityState.Detached;
            return false;
        }
    }

    public async Task AtualizarAsync(RegistroIdempotencia registro, CancellationToken ct = default)
    {
        if (_contexto.Entry(registro).State == EntityState.Detached)
            _contexto.RegistrosIdempotencia.Update(registro);

        await _contexto.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(RegistroIdempotencia registro, CancellationToken ct = default)
    {
        _contexto.RegistrosIdempotencia.Remove(registro);
        await _contexto.SaveChangesAsync(ct);
    }
}
