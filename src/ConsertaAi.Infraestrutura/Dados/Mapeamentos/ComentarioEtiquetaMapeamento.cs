using ConsertaAi.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsertaAi.Infraestrutura.Dados.Mapeamentos;

/// <summary>
/// Tabela intermediária COMENTARIO_ETIQUETA da relação N:N, com chave primária composta
/// (mesmo padrão de JogoPlataforma da aula de N:N com Oracle).
/// </summary>
public class ComentarioEtiquetaMapeamento : IEntityTypeConfiguration<ComentarioEtiqueta>
{
    public void Configure(EntityTypeBuilder<ComentarioEtiqueta> builder)
    {
        builder.ToTable("COMENTARIO_ETIQUETA");

        // Chave primária composta: as duas colunas juntas evitam relacionamentos duplicados
        builder.HasKey(ce => new { ce.ComentarioId, ce.EtiquetaId });

        builder.Property(ce => ce.ComentarioId).HasColumnName("ID_COMENTARIO");
        builder.Property(ce => ce.EtiquetaId).HasColumnName("ID_ETIQUETA");

        builder.HasOne(ce => ce.Comentario)
            .WithMany(c => c.ComentariosEtiquetas)
            .HasForeignKey(ce => ce.ComentarioId)
            .HasConstraintName("FK_COMENT_ETIQ_COMENTARIO")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ce => ce.Etiqueta)
            .WithMany(e => e.ComentariosEtiquetas)
            .HasForeignKey(ce => ce.EtiquetaId)
            .HasConstraintName("FK_COMENT_ETIQ_ETIQUETA")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
