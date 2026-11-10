using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Interfaces;

namespace ConsertaAi.Infraestrutura.Repositorios;

/// <summary>
/// Implementação em memória usada quando o MongoDB não está configurado
/// (testes de integração e execução local sem dependências). Mesmo contrato do repositório Mongo (LSP).
/// </summary>
public class MemoriaAtividadeRepositorio : IAtividadeRepositorio
{
    private readonly List<Atividade> _atividades = [];
    private readonly object _trava = new();

    public Task AdicionarAsync(Atividade atividade, CancellationToken ct = default)
    {
        lock (_trava)
            _atividades.Add(atividade);

        return Task.CompletedTask;
    }

    public Task<PagedResult<Atividade>> ObterPaginadoAsync(long ocorrenciaId, TipoAtividade? tipo, bool ascendente, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        List<Atividade> filtradas;
        lock (_trava)
        {
            filtradas = _atividades
                .Where(a => a.OcorrenciaId == ocorrenciaId && (!tipo.HasValue || a.Tipo == tipo.Value))
                .ToList();
        }

        var ordenadas = ascendente
            ? filtradas.OrderBy(a => a.DataRegistro)
            : filtradas.OrderByDescending(a => a.DataRegistro);

        var itens = ordenadas.Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToList();
        return Task.FromResult(new PagedResult<Atividade>(itens, filtradas.Count, pagina, tamanhoPagina));
    }
}
