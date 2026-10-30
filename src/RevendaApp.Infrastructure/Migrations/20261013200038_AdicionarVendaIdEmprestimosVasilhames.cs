using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevendaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarVendaIdEmprestimosVasilhames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VendaId",
                table: "EmprestimosVasilhames",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmprestimosVasilhames_VendaId",
                table: "EmprestimosVasilhames",
                column: "VendaId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmprestimosVasilhames_Vendas_VendaId",
                table: "EmprestimosVasilhames",
                column: "VendaId",
                principalTable: "Vendas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmprestimosVasilhames_Vendas_VendaId",
                table: "EmprestimosVasilhames");

            migrationBuilder.DropIndex(
                name: "IX_EmprestimosVasilhames_VendaId",
                table: "EmprestimosVasilhames");

            migrationBuilder.DropColumn(
                name: "VendaId",
                table: "EmprestimosVasilhames");
        }
    }
}