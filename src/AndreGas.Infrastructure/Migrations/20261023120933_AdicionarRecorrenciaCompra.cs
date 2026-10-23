using System;

using Microsoft.EntityFrameworkCore.Migrations;

// CA1861: o gerador do EF Core emite "new[]{...}" inline para o índice composto; suprimir no
// arquivo gerado (o código de migração não é editado manualmente).
#pragma warning disable CA1861

#nullable disable

namespace AndreGas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRecorrenciaCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvisarRecompra",
                table: "Produtos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DiasPadraoRecompra",
                table: "Produtos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RecorrenciasCompra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataUltimaCompra = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IntervaloMedioDias = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    QuantidadeCompras = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecorrenciasCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecorrenciasCompra_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecorrenciasCompra_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecorrenciasCompra_ClienteId_ProdutoId",
                table: "RecorrenciasCompra",
                columns: new[] { "ClienteId", "ProdutoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecorrenciasCompra_ProdutoId",
                table: "RecorrenciasCompra",
                column: "ProdutoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecorrenciasCompra");

            migrationBuilder.DropColumn(
                name: "AvisarRecompra",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "DiasPadraoRecompra",
                table: "Produtos");
        }
    }
}
