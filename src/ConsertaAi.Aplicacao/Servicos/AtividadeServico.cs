using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Observabilidade;
using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConsertaAi.Aplicacao.Servicos;

/// <summary>
/// Linha do tempo (MongoDB). O registro é "best effort": se o MongoDB estiver fora,
/// o apoio/comentário do cidadão continua sendo salvo e o problema aparece no /health e nos logs.
/// </summary>
public sealed class AtividadeServico : IAtividadeServico
{
    private readonly IAtividadeRepositorio _repositorio;
    private readonly ILogger<AtividadeServico> _logger;

    public AtividadeServico(IAtividadeRepositorio repositorio, ILogger<AtividadeServico> logger)
    {
        _repositorio = repositorio;
        _logger = logger;
    }

    public async Task RegistrarAsync(long ocorrenciaId, TipoAtividade tipo, string usuarioId, string descricao, Guid? recursoId, CancellationToken ct = default)
    {
        try
        {
            await _repositorio.AdicionarAsync(new Atividade(ocorrenciaId, tipo, usuarioId, descricao, recursoId), ct);
        }
        catch (Exception ex)
        {
            AplicacaoMetricas.IntegracoesFalhasContador.Add(1, AplicacaoMetricas.Tag("integracao", "mongodb"));
            _logger.LogError(ex, "Falha ao registrar atividade {Tipo} da ocorrência {OcorrenciaId} no MongoDB", tipo, ocorrenciaId);
        }
    }

    public async Task<PagedResult<AtividadeResponse>> ListarAsync(long ocorrenciaId, AtividadeFiltroRequest filtro, CancellationToken ct = default)
    {
        var pagina = await _repositorio.ObterPaginadoAsync(ocorrenciaId, filtro.Tipo, filtro.Ascendente, filtro.Pagina, filtro.TamanhoPagina, ct);
        return pagina.Mapear(AtividadeResponse.De);
    }
}
