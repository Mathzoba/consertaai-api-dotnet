using ConsertaAi.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsertaAi.Infraestrutura.Dados.Mapeamentos;

/// <summary>Tabela COMENTARIO.</summary>
public class ComentarioMapeamento : IEntityTypeConfiguration<Comentario>
{
    public void Configure(EntityTypeBuilder<Comentario> builder)
    {
        builder.ToTable("COMENTARIO");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(c => c.OcorrenciaId).HasColumnName("ID_OCORRENCIA").IsRequired();
        builder.Property(c => c.UsuarioId).HasColumnName("ID_USUARIO").HasMaxLength(64).IsRequired();
        builder.Property(c => c.Texto).HasColumnName("TEXTO").HasMaxLength(Comentario.TamanhoMaximoTexto).IsRequired();
        builder.Property(c => c.DataCriacao).HasColumnName("DT_CRIACAO").IsRequired();
        builder.Property(c => c.DataEdicao).HasColumnName("DT_EDICAO");

        // Propriedades calculadas não viram colunas
        builder.Ignore(c => c.Etiquetas);
        builder.Ignore(c => c.Editado);

        builder.HasIndex(c => new { c.OcorrenciaId, c.DataCriacao }).HasDatabaseName("IX_COMENTARIO_OCORRENCIA_DATA");
    }
}
