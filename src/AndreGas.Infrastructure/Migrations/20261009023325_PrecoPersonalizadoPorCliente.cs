using System;

using Microsoft.EntityFrameworkCore.Migrations;

// CA1861: o gerador do EF Core emite "new[]{...}" inline para o índice composto; suprimir no
// arquivo gerado (o código de migração não é editado manualmente).
#pragma warning disable CA1861

#nullable disable

namespace AndreGas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrecoPersonalizadoPorCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrecosPersonalizadosClientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecosPersonalizadosClientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrecosPersonalizadosClientes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrecosPersonalizadosClientes_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrecosPersonalizadosClientes_ClienteId_ProdutoId",
                table: "PrecosPersonalizadosClientes",
                columns: new[] { "ClienteId", "ProdutoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrecosPersonalizadosClientes_ProdutoId",
                table: "PrecosPersonalizadosClientes",
                column: "ProdutoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrecosPersonalizadosClientes");
        }
    }
}