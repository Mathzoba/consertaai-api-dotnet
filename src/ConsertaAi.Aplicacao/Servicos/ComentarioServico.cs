using System.Diagnostics;
using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Aplicacao.Moderacao;
using ConsertaAi.Aplicacao.Observabilidade;
using ConsertaAi.Aplicacao.Seguranca;
using ConsertaAi.Dominio.Comum;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConsertaAi.Aplicacao.Servicos;

/// <summary>
/// Casos de uso de comentários: moderação (Strategy), etiquetas (N:N),
/// regra de autoria (só o autor edita; autor ou gestor removem) e linha do tempo.
/// </summary>
public sealed class ComentarioServico : IComentarioServico
{
    private readonly IComentarioRepositorio _comentarios;
    private readonly IEtiquetaRepositorio _etiquetas;
    private readonly IModeradorComentario _moderador;
    private readonly IConsultaOcorrencia _consultaOcorrencia;
    private readonly IAtividadeServico _atividades;
    private readonly ILogger<ComentarioServico> _logger;

    public ComentarioServico(
        IComentarioRepositorio comentarios,
        IEtiquetaRepositorio etiquetas,
        IModeradorComentario moderador,
        IConsultaOcorrencia consultaOcorrencia,
        IAtividadeServico atividades,
        ILogger<ComentarioServico> logger)
    {
        _comentarios = comentarios;
        _etiquetas = etiquetas;
        _moderador = moderador;
        _consultaOcorrencia = consultaOcorrencia;
        _atividades = atividades;
        _logger = logger;
    }

    public async Task<PagedResult<ComentarioResponse>> ListarPorOcorrenciaAsync(long ocorrenciaId, PaginacaoRequest paginacao, CancellationToken ct = default)
    {
        var pagina = await _comentarios.ListarPorOcorrenciaAsync(ocorrenciaId, paginacao.Pagina, paginacao.TamanhoPagina, paginacao.Ascendente, ct);
        return pagina.Mapear(ComentarioResponse.De);
    }

    public async Task<Resultado<ComentarioResponse>> ObterPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var comentario = await _comentarios.ObterPorIdAsync(id, ct);
        return comentario is null
            ? Erro.NaoEncontrado("Comentário não encontrado.")
            : ComentarioResponse.De(comentario);
    }

    public async Task<Resultado<ComentarioResponse>> CriarAsync(CriarComentarioRequest request, UsuarioAutenticado usuario, CancellationToken ct = default)
    {
        using var activity = AplicacaoMetricas.ActivitySourceAplicacao.StartActivity("CriarComentario");
        activity?.SetTag("ocorrencia.id", request.OcorrenciaId);

        var moderacao = _moderador.Moderar(request.Texto);
        if (!moderacao.Aprovado)
            return Falhar(activity, "criar", "moderacao", Erro.Validacao("texto", moderacao.Motivos.ToArray()));

        var consulta = await _consultaOcorrencia.ConsultarAsync(request.OcorrenciaId, ct);
        if (consulta.Situacao == SituacaoConsultaOcorrencia.NaoEncontrada)
            return Falhar(activity, "criar", "ocorrencia_inexistente", Erro.NaoEncontrado($"Ocorrência {request.OcorrenciaId} não encontrada."));

        if (!consulta.PermiteComentario)
            return Falhar(activity, "criar", "ocorrencia_cancelada", Erro.RegraNegocio("Não é possível comentar uma ocorrência cancelada."));

        var etiquetas = await CarregarEtiquetasAsync(request.EtiquetaIds, ct);
        if (!etiquetas.Sucesso)
            return Falhar(activity, "criar", "etiqueta_inexistente", etiquetas.Erro!);

        var comentario = new Comentario(request.OcorrenciaId, usuario.Id, request.Texto);
        comentario.DefinirEtiquetas(etiquetas.Valor!);
        await _comentarios.AdicionarAsync(comentario, ct);

        await _atividades.RegistrarAsync(comentario.OcorrenciaId, TipoAtividade.ComentarioCriado, usuario.Id,
            "Comentário publicado.", comentario.Id, ct);

        AplicacaoMetricas.ComentariosContador.Add(1, AplicacaoMetricas.Tag("operacao", "criar"), AplicacaoMetricas.Tag("status", "sucesso"));
        _logger.LogInformation("Comentário {ComentarioId} criado na ocorrência {OcorrenciaId} pelo usuário {UsuarioId}",
            comentario.Id, comentario.OcorrenciaId, usuario.Id);

        return ComentarioResponse.De(comentario);
    }

    public async Task<Resultado<ComentarioResponse>> AtualizarAsync(Guid id, AtualizarComentarioRequest request, UsuarioAutenticado usuario, CancellationToken ct = default)
    {
        using var activity = AplicacaoMetricas.ActivitySourceAplicacao.StartActivity("AtualizarComentario");
        activity?.SetTag("comentario.id", id);

        var comentario = await _comentarios.ObterPorIdAsync(id, ct);
        if (comentario is null)
            return Falhar(activity, "atualizar", "inexistente", Erro.NaoEncontrado("Comentário não encontrado."));

        if (!comentario.PodeSerEditadoPor(usuario.Id))
        {
            _logger.LogWarning("Usuário {UsuarioId} tentou editar o comentário {ComentarioId} de outro autor", usuario.Id, id);
            return Falhar(activity, "atualizar", "nao_autor", Erro.Proibido("Somente o autor pode editar o comentário."));
        }

        var moderacao = _moderador.Moderar(request.Texto);
        if (!moderacao.Aprovado)
            return Falhar(activity, "atualizar", "moderacao", Erro.Validacao("texto", moderacao.Motivos.ToArray()));

        if (request.EtiquetaIds is not null)
        {
            var etiquetas = await CarregarEtiquetasAsync(request.EtiquetaIds, ct);
            if (!etiquetas.Sucesso)
                return Falhar(activity, "atualizar", "etiqueta_inexistente", etiquetas.Erro!);

            comentario.DefinirEtiquetas(etiquetas.Valor!);
        }

        comentario.Editar(usuario.Id, request.Texto);
        await _comentarios.AtualizarAsync(comentario, ct);

        await _atividades.RegistrarAsync(comentario.OcorrenciaId, TipoAtividade.ComentarioEditado, usuario.Id,
            "Comentário editado pelo autor.", comentario.Id, ct);

        AplicacaoMetricas.ComentariosContador.Add(1, AplicacaoMetricas.Tag("operacao", "atualizar"), AplicacaoMetricas.Tag("status", "sucesso"));
        _logger.LogInformation("Comentário {ComentarioId} atualizado pelo autor {UsuarioId}", id, usuario.Id);

        return ComentarioResponse.De(comentario);
    }

    public async Task<Resultado> RemoverAsync(Guid id, UsuarioAutenticado usuario, CancellationToken ct = default)
    {
        using var activity = AplicacaoMetricas.ActivitySourceAplicacao.StartActivity("RemoverComentario");
        activity?.SetTag("comentario.id", id);

        var comentario = await _comentarios.ObterPorIdAsync(id, ct);
        if (comentario is null)
            return Falhar(activity, "remover", "inexistente", Erro.NaoEncontrado("Comentário não encontrado."));

        if (!comentario.PodeSerRemovidoPor(usuario.Id, usuario.EhGestor))
        {
            _logger.LogWarning("Usuário {UsuarioId} tentou remover o comentário {ComentarioId} sem permissão", usuario.Id, id);
            return Falhar(activity, "remover", "sem_permissao", Erro.Proibido("Somente o autor ou um gestor podem remover o comentário."));
        }

        await _comentarios.RemoverAsync(comentario, ct);

        var descricao = comentario.EhAutor(usuario.Id) ? "Comentário removido pelo autor." : "Comentário removido pela moderação (gestor).";
        await _atividades.RegistrarAsync(comentario.OcorrenciaId, TipoAtividade.ComentarioRemovido, usuario.Id, descricao, comentario.Id, ct);

        AplicacaoMetricas.ComentariosContador.Add(1, AplicacaoMetricas.Tag("operacao", "remover"), AplicacaoMetricas.Tag("status", "sucesso"));
        _logger.LogInformation("Comentário {ComentarioId} removido pelo usuário {UsuarioId}", id, usuario.Id);

        return Resultado.Ok();
    }

    private async Task<Resultado<IReadOnlyList<Etiqueta>>> CarregarEtiquetasAsync(IEnumerable<Guid>? ids, CancellationToken ct)
    {
        var distintos = (ids ?? []).Distinct().ToList();
        if (distintos.Count == 0)
            return Resultado<IReadOnlyList<Etiqueta>>.Ok([]);

        var encontradas = await _etiquetas.ObterPorIdsAsync(distintos, ct);
        var inexistentes = distintos.Except(encontradas.Select(e => e.Id)).ToList();

        return inexistentes.Count == 0
            ? Resultado<IReadOnlyList<Etiqueta>>.Ok(encontradas)
            : Erro.Validacao("etiquetaIds", $"Etiqueta(s) inexistente(s): {string.Join(", ", inexistentes)}.");
    }

    private static Erro Falhar(Activity? activity, string operacao, string motivo, Erro erro)
    {
        activity?.SetStatus(ActivityStatusCode.Error, motivo);
        AplicacaoMetricas.ComentariosContador.Add(1, AplicacaoMetricas.Tag("operacao", operacao), AplicacaoMetricas.Tag("status", motivo));
        AplicacaoMetricas.ErrosContador.Add(1, AplicacaoMetricas.Tag("tipo", $"comentario_{motivo}"));
        return erro;
    }
}
