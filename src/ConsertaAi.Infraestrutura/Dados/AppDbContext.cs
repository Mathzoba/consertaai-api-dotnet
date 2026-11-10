using ConsertaAi.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace ConsertaAi.Infraestrutura.Dados;

/// <summary>
/// Ponte entre o sistema, o Entity Framework Core e o banco relacional (Oracle ou Azure SQL).
/// As configurações de cada tabela ficam em classes IEntityTypeConfiguration (pasta Mapeamentos),
/// mantendo o DbContext pequeno (SRP).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Apoio> Apoios => Set<Apoio>();
    public DbSet<Comentario> Comentarios => Set<Comentario>();
    public DbSet<Etiqueta> Etiquetas => Set<Etiqueta>();
    public DbSet<ComentarioEtiqueta> ComentariosEtiquetas => Set<ComentarioEtiqueta>();
    public DbSet<RegistroIdempotencia> RegistrosIdempotencia => Set<RegistroIdempotencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // No Oracle, texto sem limite viraria NVARCHAR2(2000); a resposta armazenada pode ser maior → NCLOB
        if (Database.ProviderName?.Contains("Oracle", StringComparison.OrdinalIgnoreCase) == true)
        {
            modelBuilder.Entity<RegistroIdempotencia>()
                .Property(r => r.CorpoResposta)
                .HasColumnType("NCLOB");
        }
    }
}
