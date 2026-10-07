using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndreGas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarValorEntregaEmVendas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ValorEntrega",
                table: "Vendas",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValorEntrega",
                table: "Vendas");
        }
    }
}
