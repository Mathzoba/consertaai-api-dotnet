using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConsertaAi.Infraestrutura.Dados;

/// <summary>
/// Fábrica usada somente em tempo de design pelas ferramentas do EF Core:
/// <list type="bullet">
/// <item>"dotnet ef migrations add" → gera as migrations para Oracle (padrão);</item>
/// <item>EF_PROVEDOR=SqlServer + "dotnet ef dbcontext script" → gera o DDL do Azure SQL (scripts/script-bd.sql).</item>
/// </list>
/// Nenhuma conexão é aberta nesses comandos, por isso a connection string é apenas um marcador sem credenciais.
/// Em execução, a API usa a connection string real do ambiente (ConnectionStrings__ConsertaAi).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();

        if (string.Equals(Environment.GetEnvironmentVariable("EF_PROVEDOR"), "SqlServer", StringComparison.OrdinalIgnoreCase))
            builder.UseSqlServer("Server=design-time;Database=ConsertaAi;Integrated Security=true");
        else
            builder.UseOracle("Data Source=design-time:1521/FREEPDB1",
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));

        return new AppDbContext(builder.Options);
    }
}
