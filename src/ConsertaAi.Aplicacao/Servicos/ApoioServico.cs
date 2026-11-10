using System.Diagnostics;
using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Aplicacao.Integracoes;
using ConsertaAi.Aplicacao.Observabilidade;
using ConsertaAi.Aplicacao.Seguranca;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;
using ConsertaAi.Dominio.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConsertaAi.Aplicacao.Servicos;

/// <summary>
/// Orquestra o apoio a ocorrências: valida a ocorrência na API Java, impede apoio duplicado,
/// persiste, publica o evento "apoio.registrado" e registra a atividade no MongoDB.
/// </summary>
public sealed class ApoioServico : IApoioServico
{
    private readonly IApoioRepositorio _repositorio;
    private readonly IConsultaOcorrencia _consultaOcorrencia;
    private readonly IPublicadorEventos _publicador;
    private readonly IAtividadeServico _atividades;
    private readonly ILogger<ApoioServico> _logger;

    public ApoioServico(
        IApoioRepositorio repositorio,
        IConsultaOcorrencia consultaOcorrencia,
        IPublicadorEventos publicador,
        IAtividadeServico atividades,
        ILogger<ApoioServico> logger)
    {
        _repositorio = repositorio;
        _consultaOcorrencia = consultaOcorrencia;
        _publicador = publicador;
        _atividades = atividades;
        _logger = logger;
    }

    public async Task<Resultado<ApoioResponse>> RegistrarAsync(RegistrarApoioRequest request, UsuarioAutenticado usuario, CancellationToken ct = default)
    {
        // Span customizado para rastrear o caso de uso no tracing distribuído
        using var activity = AplicacaoMetricas.ActivitySourceAplicacao.StartActivity("RegistrarApoio");
        activity?.SetTag("ocorrencia.id", request.OcorrenciaId);
        activity?.SetTag("usuario.id", usuario.Id);

        var consulta = await _consultaOcorrencia.ConsultarAsync(request.OcorrenciaId, ct);
        if (consulta.Situacao == SituacaoConsultaOcorrencia.NaoEncontrada)
            return Rejeitar(activity, "ocorrencia_inexistente", Erro.NaoEncontrado($"Ocorrência {request.OcorrenciaId} não encontrada."));

        if (!consulta.PermiteApoio)
            return Rejeitar(activity, "status_invalido",
                Erro.RegraNegocio($"A ocorrência {request.OcorrenciaId} está com status {consulta.Status} e não aceita novos apoios."));

        if (await _repositorio.ExisteAsync(request.OcorrenciaId, usuario.Id, ct))
            return Duplicado(activity, request.OcorrenciaId, usuario.Id);

        var apoio = new Apoio(request.OcorrenciaId, usuario.Id);

        // Segunda barreira: índice único no banco protege contra requisições simultâneas
        if (!await _repositorio.AdicionarAsync(apoio, ct))
            return Duplicado(activity, request.OcorrenciaId, usuario.Id);

        var total = await _repositorio.ContarPorOcorrenciaAsync(request.OcorrenciaId, ct);

        await _publicador.PublicarAsync(ApoioAlteradoEvento.Registrado(request.OcorrenciaId, usuario.Id, total), ct);
        await _atividades.RegistrarAsync(request.OcorrenciaId, TipoAtividade.ApoioRegistrado, usuario.Id,
            $"Apoio registrado. Total de apoios: {total}.", apoio.Id, ct);

        AplicacaoMetricas.ApoiosRegistradosContador.Add(1, AplicacaoMetricas.Tag("status", "sucesso"));
        activity?.SetTag("apoios.total", total);
        _logger.LogInformation("Apoio {ApoioId} registrado na ocorrência {OcorrenciaId} pelo usuário {UsuarioId}. Total: {TotalApoios}",
            apoio.Id, request.OcorrenciaId, usuario.Id, total);

        return ApoioResponse.De(apoio, total);
    }

    public async Task<Resultado> RetirarAsync(long ocorrenciaId, UsuarioAutenticado usuario, CancellationToken ct = default)
    {
        using var activity = AplicacaoMetricas.ActivitySourceAplicacao.StartActivity("RetirarApoio");
        activity?.SetTag("ocorrencia.id", ocorrenciaId);

        var apoio = await _repositorio.ObterPorOcorrenciaEUsuarioAsync(ocorrenciaId, usuario.Id, ct);
        if (apoio is null)
        {
            _logger.LogWarning("Usuário {UsuarioId} tentou retirar apoio inexistente da ocorrência {OcorrenciaId}", usuario.Id, ocorrenciaId);
            return Erro.NaoEncontrado("Você não apoiou esta ocorrência.");
        }

        await _repositorio.RemoverAsync(apoio, ct);
        var total = await _repositorio.ContarPorOcorrenciaAsync(ocorrenciaId, ct);

        await _publicador.PublicarAsync(ApoioAlteradoEvento.Removido(ocorrenciaId, usuario.Id, total), ct);
        await _atividades.RegistrarAsync(ocorrenciaId, TipoAtividade.ApoioRetirado, usuario.Id,
            $"Apoio retirado. Total de apoios: {total}.", apoio.Id, ct);

        AplicacaoMetricas.ApoiosRetiradosContador.Add(1);
        _logger.LogInformation("Apoio {ApoioId} retirado da ocorrência {OcorrenciaId}. Total: {TotalApoios}", apoio.Id, ocorrenciaId, total);

        return Resultado.Ok();
    }

    public async Task<ResumoApoiosResponse> ObterResumoAsync(long ocorrenciaId, UsuarioAutenticado usuario, CancellationToken ct = default)
    {
        var total = await _repositorio.ContarPorOcorrenciaAsync(ocorrenciaId, ct);
        var apoiou = await _repositorio.ExisteAsync(ocorrenciaId, usuario.Id, ct);
        return new ResumoApoiosResponse(ocorrenciaId, total, apoiou);
    }

    private Erro Duplicado(Activity? activity, long ocorrenciaId, string usuarioId)
    {
        _logger.LogWarning("Apoio duplicado bloqueado: usuário {UsuarioId} já apoiou a ocorrência {OcorrenciaId}", usuarioId, ocorrenciaId);
        return Rejeitar(activity, "duplicado", Erro.Conflito("Você já apoiou esta ocorrência."), "duplicado");
    }

    private static Erro Rejeitar(Activity? activity, string motivo, Erro erro, string status = "rejeitado")
    {
        activity?.SetStatus(ActivityStatusCode.Error, motivo);
        AplicacaoMetricas.ApoiosRegistradosContador.Add(1, AplicacaoMetricas.Tag("status", status));
        AplicacaoMetricas.ErrosContador.Add(1, AplicacaoMetricas.Tag("tipo", $"apoio_{motivo}"));
        return erro;
    }
}
