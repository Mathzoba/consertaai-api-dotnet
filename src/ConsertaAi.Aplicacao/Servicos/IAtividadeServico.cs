using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Enums;

namespace ConsertaAi.Aplicacao.Servicos;

/// <summary>Linha do tempo de participação armazenada no MongoDB.</summary>
public interface IAtividadeServico
{
    /// <summary>Registra a atividade. Nunca lança exceção: falhas no MongoDB são apenas logadas.</summary>
    Task RegistrarAsync(long ocorrenciaId, TipoAtividade tipo, string usuarioId, string descricao, Guid? recursoId, CancellationToken ct = default);

    Task<PagedResult<AtividadeResponse>> ListarAsync(long ocorrenciaId, AtividadeFiltroRequest filtro, CancellationToken ct = default);
}
