using ConsertaAi.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsertaAi.Infraestrutura.Dados.Mapeamentos;

/// <summary>Tabela ETIQUETA, com etiquetas iniciais (seed) aplicadas pela migration.</summary>
public class EtiquetaMapeamento : IEntityTypeConfiguration<Etiqueta>
{
    public static readonly Guid RiscoPedestresId = Guid.Parse("8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e01");
    public static readonly Guid ProblemaRecorrenteId = Guid.Parse("8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e02");
    public static readonly Guid InformacaoNovaId = Guid.Parse("8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e03");
    public static readonly Guid PiorouId = Guid.Parse("8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e04");

    public void Configure(EntityTypeBuilder<Etiqueta> builder)
    {
        builder.ToTable("ETIQUETA");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(e => e.Nome).HasColumnName("NOME").HasMaxLength(Etiqueta.TamanhoMaximoNome).IsRequired();
        builder.Property(e => e.Descricao).HasColumnName("DESCRICAO").HasMaxLength(Etiqueta.TamanhoMaximoDescricao);
        builder.Property(e => e.DataCriacao).HasColumnName("DT_CRIACAO").IsRequired();

        builder.HasIndex(e => e.Nome).IsUnique().HasDatabaseName("UX_ETIQUETA_NOME");

        var dataSeed = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new { Id = RiscoPedestresId, Nome = "Risco a pedestres", Descricao = "O problema oferece risco imediato a quem passa pelo local.", DataCriacao = dataSeed },
            new { Id = ProblemaRecorrenteId, Nome = "Problema recorrente", Descricao = "O problema já foi resolvido antes e voltou a acontecer.", DataCriacao = dataSeed },
            new { Id = InformacaoNovaId, Nome = "Informação nova", Descricao = "Traz um dado novo para a equipe da zeladoria.", DataCriacao = dataSeed },
            new { Id = PiorouId, Nome = "Piorou", Descricao = "A situação piorou desde o report original.", DataCriacao = dataSeed });
    }
}
