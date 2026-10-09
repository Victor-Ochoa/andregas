using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndreGas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarExclusaoLogicaVendas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExcluidaEm",
                table: "Vendas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VendaId",
                table: "Pagamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_VendaId",
                table: "Pagamentos",
                column: "VendaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pagamentos_Vendas_VendaId",
                table: "Pagamentos",
                column: "VendaId",
                principalTable: "Vendas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pagamentos_Vendas_VendaId",
                table: "Pagamentos");

            migrationBuilder.DropIndex(
                name: "IX_Pagamentos_VendaId",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "ExcluidaEm",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "VendaId",
                table: "Pagamentos");
        }
    }
}
