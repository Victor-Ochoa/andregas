using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndreGas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PagamentoFormaPagamentoEVendaStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Vendas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pago");

            migrationBuilder.AddColumn<string>(
                name: "FormaPagamento",
                table: "Pagamentos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Dinheiro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "FormaPagamento",
                table: "Pagamentos");
        }
    }
}
