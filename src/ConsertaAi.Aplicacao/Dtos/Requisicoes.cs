using System.ComponentModel.DataAnnotations;
using ConsertaAi.Dominio.Entidades;
using ConsertaAi.Dominio.Enums;

namespace ConsertaAi.Aplicacao.Dtos;

/// <summary>Corpo do POST /apoios.</summary>
/// <param name="OcorrenciaId">Id da ocorrência (API Java) que o cidadão quer apoiar.</param>
public record RegistrarApoioRequest(
    [property: Range(1, long.MaxValue, ErrorMessage = "O id da ocorrência deve ser maior que zero.")]
    long OcorrenciaId);

/// <summary>Corpo do POST /comentarios.</summary>
/// <param name="OcorrenciaId">Id da ocorrência comentada.</param>
/// <param name="Texto">Texto do comentário (3 a 1000 caracteres).</param>
/// <param name="EtiquetaIds">Etiquetas opcionais (até 5) — relação N:N.</param>
public record CriarComentarioRequest(
    [property: Range(1, long.MaxValue, ErrorMessage = "O id da ocorrência deve ser maior que zero.")]
    long OcorrenciaId,
    [property: Required(ErrorMessage = "O texto do comentário é obrigatório.")]
    [property: StringLength(Comentario.TamanhoMaximoTexto, MinimumLength = Comentario.TamanhoMinimoTexto,
        ErrorMessage = "O texto do comentário deve ter entre 3 e 1000 caracteres.")]
    string Texto,
    [property: MaxLength(5, ErrorMessage = "Um comentário pode ter no máximo 5 etiquetas.")]
    List<Guid>? EtiquetaIds = null);

/// <summary>Corpo do PUT /comentarios/{id}.</summary>
/// <param name="Texto">Novo texto do comentário.</param>
/// <param name="EtiquetaIds">Se informado, substitui as etiquetas atuais; se nulo, mantém.</param>
public record AtualizarComentarioRequest(
    [property: Required(ErrorMessage = "O texto do comentário é obrigatório.")]
    [property: StringLength(Comentario.TamanhoMaximoTexto, MinimumLength = Comentario.TamanhoMinimoTexto,
        ErrorMessage = "O texto do comentário deve ter entre 3 e 1000 caracteres.")]
    string Texto,
    [property: MaxLength(5, ErrorMessage = "Um comentário pode ter no máximo 5 etiquetas.")]
    List<Guid>? EtiquetaIds = null);

/// <summary>Corpo do POST/PUT /etiquetas.</summary>
public record SalvarEtiquetaRequest(
    [property: Required(ErrorMessage = "O nome da etiqueta é obrigatório.")]
    [property: StringLength(Etiqueta.TamanhoMaximoNome, MinimumLength = Etiqueta.TamanhoMinimoNome,
        ErrorMessage = "O nome da etiqueta deve ter entre 2 e 40 caracteres.")]
    string Nome,
    [property: StringLength(Etiqueta.TamanhoMaximoDescricao, ErrorMessage = "A descrição deve ter no máximo 200 caracteres.")]
    string? Descricao = null);

/// <summary>Parâmetros de paginação lidos da query string via [AsParameters].</summary>
public record PaginacaoRequest(
    [property: Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a 1.")]
    int Pagina = 1,
    [property: Range(1, 50, ErrorMessage = "O tamanho da página deve estar entre 1 e 50.")]
    int TamanhoPagina = 10,
    bool Ascendente = false);

/// <summary>Filtros da linha do tempo (MongoDB) lidos da query string via [AsParameters].</summary>
public record AtividadeFiltroRequest(
    TipoAtividade? Tipo = null,
    bool Ascendente = false,
    [property: Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a 1.")]
    int Pagina = 1,
    [property: Range(1, 50, ErrorMessage = "O tamanho da página deve estar entre 1 e 50.")]
    int TamanhoPagina = 10);
