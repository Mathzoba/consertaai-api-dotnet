using ConsertaAi.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsertaAi.Infraestrutura.Dados.Mapeamentos;

/// <summary>Tabela APOIO. Índice único (ID_OCORRENCIA, ID_USUARIO): ninguém apoia duas vezes.</summary>
public class ApoioMapeamento : IEntityTypeConfiguration<Apoio>
{
    public void Configure(EntityTypeBuilder<Apoio> builder)
    {
        builder.ToTable("APOIO");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(a => a.OcorrenciaId).HasColumnName("ID_OCORRENCIA").IsRequired();
        builder.Property(a => a.UsuarioId).HasColumnName("ID_USUARIO").HasMaxLength(64).IsRequired();
        builder.Property(a => a.DataApoio).HasColumnName("DT_APOIO").IsRequired();

        builder.HasIndex(a => new { a.OcorrenciaId, a.UsuarioId })
            .IsUnique()
            .HasDatabaseName("UX_APOIO_OCORRENCIA_USUARIO");
    }
}
