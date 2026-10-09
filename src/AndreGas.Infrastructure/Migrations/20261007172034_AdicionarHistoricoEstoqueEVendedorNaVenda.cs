using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndreGas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarHistoricoEstoqueEVendedorNaVenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VendedorId",
                table: "Vendas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HistoricosEstoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Usuario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    VendaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricosEstoque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricosEstoque_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosEstoque_ProdutoId",
                table: "HistoricosEstoque",
                column: "ProdutoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricosEstoque");

            migrationBuilder.DropColumn(
                name: "VendedorId",
                table: "Vendas");
        }
    }
}