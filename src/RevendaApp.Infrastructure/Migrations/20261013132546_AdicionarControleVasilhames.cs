using System;

using Microsoft.EntityFrameworkCore.Migrations;

// CA1861: o gerador do EF Core emite "new[]{...}" inline para o índice composto; suprimir no
// arquivo gerado (o código de migração não é editado manualmente).
#pragma warning disable CA1861

#nullable disable

namespace RevendaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarControleVasilhames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ControlaVasilhame",
                table: "Produtos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeVasilhames",
                table: "Produtos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeVasilhamesEmprestados",
                table: "Produtos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EhEmprestimo",
                table: "ItensVenda",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EmprestimosVasilhames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observacao = table.Column<string>(type: "text", nullable: true),
                    QuantidadeDevolvida = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DevolvidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmprestimosVasilhames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmprestimosVasilhames_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmprestimosVasilhames_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmprestimosVasilhames_ClienteId_ProdutoId",
                table: "EmprestimosVasilhames",
                columns: new[] { "ClienteId", "ProdutoId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmprestimosVasilhames_ProdutoId",
                table: "EmprestimosVasilhames",
                column: "ProdutoId");

            migrationBuilder.Sql(
                "UPDATE \"Produtos\" SET \"ControlaVasilhame\" = true WHERE \"Nome\" IN ('Botijão Gás 13kg','Botijão Gás 45kg','Galão Água 20L');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmprestimosVasilhames");

            migrationBuilder.DropColumn(
                name: "ControlaVasilhame",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "QuantidadeVasilhames",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "QuantidadeVasilhamesEmprestados",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "EhEmprestimo",
                table: "ItensVenda");
        }
    }
}