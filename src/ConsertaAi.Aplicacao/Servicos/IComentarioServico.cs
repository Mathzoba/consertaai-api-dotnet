using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Seguranca;
using ConsertaAi.Dominio.Comum;

namespace ConsertaAi.Aplicacao.Servicos;

/// <summary>Casos de uso de comentários em ocorrências.</summary>
public interface IComentarioServico
{
    Task<PagedResult<ComentarioResponse>> ListarPorOcorrenciaAsync(long ocorrenciaId, PaginacaoRequest paginacao, CancellationToken ct = default);
    Task<Resultado<ComentarioResponse>> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Resultado<ComentarioResponse>> CriarAsync(CriarComentarioRequest request, UsuarioAutenticado usuario, CancellationToken ct = default);
    Task<Resultado<ComentarioResponse>> AtualizarAsync(Guid id, AtualizarComentarioRequest request, UsuarioAutenticado usuario, CancellationToken ct = default);
    Task<Resultado> RemoverAsync(Guid id, UsuarioAutenticado usuario, CancellationToken ct = default);
}
