using ConsertaAi.Aplicacao.Comum;
using ConsertaAi.Aplicacao.Dtos;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConsertaAi.Aplicacao.Servicos;

/// <summary>CRUD de etiquetas, com nome único (sem diferenciar maiúsculas).</summary>
public sealed class EtiquetaServico : IEtiquetaServico
{
    private readonly IEtiquetaRepositorio _repositorio;
    private readonly ILogger<EtiquetaServico> _logger;

    public EtiquetaServico(IEtiquetaRepositorio repositorio, ILogger<EtiquetaServico> logger)
    {
        _repositorio = repositorio;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EtiquetaResponse>> ListarAsync(CancellationToken ct = default) =>
        (await _repositorio.ListarAsync(ct)).Select(EtiquetaResponse.De).ToList();

    public async Task<Resultado<EtiquetaResponse>> ObterPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var etiqueta = await _repositorio.ObterPorIdAsync(id, ct);
        return etiqueta is null ? Erro.NaoEncontrado("Etiqueta não encontrada.") : EtiquetaResponse.De(etiqueta);
    }

    public async Task<Resultado<EtiquetaResponse>> CriarAsync(SalvarEtiquetaRequest request, CancellationToken ct = default)
    {
        if (await _repositorio.ExisteComNomeAsync(request.Nome, null, ct))
            return Erro.Conflito($"Já existe uma etiqueta com o nome '{request.Nome.Trim()}'.");

        var etiqueta = new Etiqueta(request.Nome, request.Descricao);
        await _repositorio.AdicionarAsync(etiqueta, ct);

        _logger.LogInformation("Etiqueta {EtiquetaId} ({Nome}) criada", etiqueta.Id, etiqueta.Nome);
        return EtiquetaResponse.De(etiqueta);
    }

    public async Task<Resultado<EtiquetaResponse>> AtualizarAsync(Guid id, SalvarEtiquetaRequest request, CancellationToken ct = default)
    {
        var etiqueta = await _repositorio.ObterPorIdAsync(id, ct);
        if (etiqueta is null)
            return Erro.NaoEncontrado("Etiqueta não encontrada.");

        if (await _repositorio.ExisteComNomeAsync(request.Nome, id, ct))
            return Erro.Conflito($"Já existe uma etiqueta com o nome '{request.Nome.Trim()}'.");

        etiqueta.Atualizar(request.Nome, request.Descricao);
        await _repositorio.AtualizarAsync(etiqueta, ct);

        _logger.LogInformation("Etiqueta {EtiquetaId} atualizada", id);
        return EtiquetaResponse.De(etiqueta);
    }

    public async Task<Resultado> RemoverAsync(Guid id, CancellationToken ct = default)
    {
        var etiqueta = await _repositorio.ObterPorIdAsync(id, ct);
        if (etiqueta is null)
            return Erro.NaoEncontrado("Etiqueta não encontrada.");

        await _repositorio.RemoverAsync(etiqueta, ct);
        _logger.LogInformation("Etiqueta {EtiquetaId} removida", id);
        return Resultado.Ok();
    }
}
