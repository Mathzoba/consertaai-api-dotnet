using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ConsertaAi.Infraestrutura.Dados.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "APOIO",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ID_OCORRENCIA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_USUARIO = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    DT_APOIO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APOIO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "COMENTARIO",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ID_OCORRENCIA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_USUARIO = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    TEXTO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_EDICAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMENTARIO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ETIQUETA",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NOME = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ETIQUETA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IDEMPOTENCY_KEY",
                columns: table => new
                {
                    CHAVE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    ID_USUARIO = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    ENDPOINT = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    HASH_CORPO = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    STATUS_RESPOSTA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CORPO_RESPOSTA = table.Column<string>(type: "NCLOB", nullable: true),
                    TIPO_CONTEUDO = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    LOCALIZACAO = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: true),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDEMPOTENCY_KEY", x => x.CHAVE);
                });

            migrationBuilder.CreateTable(
                name: "COMENTARIO_ETIQUETA",
                columns: table => new
                {
                    ID_COMENTARIO = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ID_ETIQUETA = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMENTARIO_ETIQUETA", x => new { x.ID_COMENTARIO, x.ID_ETIQUETA });
                    table.ForeignKey(
                        name: "FK_COMENT_ETIQ_COMENTARIO",
                        column: x => x.ID_COMENTARIO,
                        principalTable: "COMENTARIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_COMENT_ETIQ_ETIQUETA",
                        column: x => x.ID_ETIQUETA,
                        principalTable: "ETIQUETA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ETIQUETA",
                columns: new[] { "ID", "DT_CRIACAO", "DESCRICAO", "NOME" },
                values: new object[,]
                {
                    { new Guid("8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e01"), new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), "O problema oferece risco imediato a quem passa pelo local.", "Risco a pedestres" },
                    { new Guid("8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e02"), new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), "O problema já foi resolvido antes e voltou a acontecer.", "Problema recorrente" },
                    { new Guid("8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e03"), new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), "Traz um dado novo para a equipe da zeladoria.", "Informação nova" },
                    { new Guid("8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e04"), new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), "A situação piorou desde o report original.", "Piorou" }
                });

            migrationBuilder.CreateIndex(
                name: "UX_APOIO_OCORRENCIA_USUARIO",
                table: "APOIO",
                columns: new[] { "ID_OCORRENCIA", "ID_USUARIO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COMENTARIO_OCORRENCIA_DATA",
                table: "COMENTARIO",
                columns: new[] { "ID_OCORRENCIA", "DT_CRIACAO" });

            migrationBuilder.CreateIndex(
                name: "IX_COMENTARIO_ETIQUETA_ID_ETIQUETA",
                table: "COMENTARIO_ETIQUETA",
                column: "ID_ETIQUETA");

            migrationBuilder.CreateIndex(
                name: "UX_ETIQUETA_NOME",
                table: "ETIQUETA",
                column: "NOME",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "APOIO");

            migrationBuilder.DropTable(
                name: "COMENTARIO_ETIQUETA");

            migrationBuilder.DropTable(
                name: "IDEMPOTENCY_KEY");

            migrationBuilder.DropTable(
                name: "COMENTARIO");

            migrationBuilder.DropTable(
                name: "ETIQUETA");
        }
    }
}
