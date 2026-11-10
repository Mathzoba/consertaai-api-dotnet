using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;

namespace ConsertaAi.Aplicacao.Dtos;

/// <summary>Apoio registrado, com o total atualizado de apoios da ocorrência.</summary>
public record ApoioResponse(Guid Id, long OcorrenciaId, string UsuarioId, DateTime DataApoio, int TotalApoios)
{
    public static ApoioResponse De(Apoio apoio, int totalApoios) =>
        new(apoio.Id, apoio.OcorrenciaId, apoio.UsuarioId, apoio.DataApoio, totalApoios);
}

/// <summary>Resumo de apoios de uma ocorrência para o usuário autenticado.</summary>
public record ResumoApoiosResponse(long OcorrenciaId, int TotalApoios, bool UsuarioApoiou);

/// <summary>Etiqueta usada para classificar comentários.</summary>
public record EtiquetaResponse(Guid Id, string Nome, string? Descricao)
{
    public static EtiquetaResponse De(Etiqueta etiqueta) => new(etiqueta.Id, etiqueta.Nome, etiqueta.Descricao);
}

/// <summary>Comentário com suas etiquetas (relação N:N).</summary>
public record ComentarioResponse(
    Guid Id,
    long OcorrenciaId,
    string UsuarioId,
    string Texto,
    DateTime DataCriacao,
    DateTime? DataEdicao,
    bool Editado,
    IReadOnlyList<EtiquetaResponse> Etiquetas)
{
    public static ComentarioResponse De(Comentario comentario) => new(
        comentario.Id,
        comentario.OcorrenciaId,
        comentario.UsuarioId,
        comentario.Texto,
        comentario.DataCriacao,
        comentario.DataEdicao,
        comentario.Editado,
        comentario.Etiquetas.OrderBy(e => e.Nome).Select(EtiquetaResponse.De).ToList());
}

/// <summary>Item da linha do tempo de participação (documento MongoDB).</summary>
public record AtividadeResponse(Guid Id, long OcorrenciaId, TipoAtividade Tipo, string UsuarioId, string Descricao, Guid? RecursoId, DateTime DataRegistro)
{
    public static AtividadeResponse De(Atividade a) =>
        new(a.Id, a.OcorrenciaId, a.Tipo, a.UsuarioId, a.Descricao, a.RecursoId, a.DataRegistro);
}
