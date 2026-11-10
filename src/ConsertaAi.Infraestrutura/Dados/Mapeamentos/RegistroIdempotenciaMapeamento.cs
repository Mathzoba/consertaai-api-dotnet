using ConsertaAi.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsertaAi.Infraestrutura.Dados.Mapeamentos;

/// <summary>Tabela IDEMPOTENCY_KEY: guarda a resposta de um POST para devolvê-la igual se ele for repetido.</summary>
public class RegistroIdempotenciaMapeamento : IEntityTypeConfiguration<RegistroIdempotencia>
{
    public void Configure(EntityTypeBuilder<RegistroIdempotencia> builder)
    {
        builder.ToTable("IDEMPOTENCY_KEY");
        builder.HasKey(r => r.Chave);

        builder.Property(r => r.Chave).HasColumnName("CHAVE").HasMaxLength(RegistroIdempotencia.TamanhoMaximoChave);
        builder.Property(r => r.UsuarioId).HasColumnName("ID_USUARIO").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Endpoint).HasColumnName("ENDPOINT").HasMaxLength(200).IsRequired();
        builder.Property(r => r.HashCorpo).HasColumnName("HASH_CORPO").HasMaxLength(64).IsRequired();
        builder.Property(r => r.StatusResposta).HasColumnName("STATUS_RESPOSTA").IsRequired();
        // Sem tamanho máximo: vira NCLOB no Oracle e NVARCHAR(MAX) no SQL Server
        builder.Property(r => r.CorpoResposta).HasColumnName("CORPO_RESPOSTA");
        builder.Property(r => r.TipoConteudo).HasColumnName("TIPO_CONTEUDO").HasMaxLength(100);
        builder.Property(r => r.Localizacao).HasColumnName("LOCALIZACAO").HasMaxLength(300);
        builder.Property(r => r.DataCriacao).HasColumnName("DT_CRIACAO").IsRequired();

        builder.Ignore(r => r.EmProcessamento);
    }
}
