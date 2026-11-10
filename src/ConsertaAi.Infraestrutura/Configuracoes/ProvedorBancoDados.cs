namespace ConsertaAi.Infraestrutura.Configuracoes;

/// <summary>Provedores relacionais suportados pelo EF Core nesta API.</summary>
public enum ProvedorBancoDados
{
    /// <summary>Oracle (padrão das aulas / FIAP) — aplica as Migrations do EF Core.</summary>
    Oracle,
    /// <summary>SQL Server / Azure SQL (deploy no Azure pelo DevOps) — cria o schema via EnsureCreated.</summary>
    SqlServer,
    /// <summary>Banco em memória (testes de integração e demonstração rápida).</summary>
    InMemory
}
