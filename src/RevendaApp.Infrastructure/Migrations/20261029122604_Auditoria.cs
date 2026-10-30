using System;

using Microsoft.EntityFrameworkCore.Migrations;

// CA1861/CA1825: o gerador do EF Core emite "new[]{...}"/"new bool[0]" inline nos índices;
// suprimir no arquivo gerado (o código de migração não é editado manualmente).
#pragma warning disable CA1861, CA1825

#nullable disable

namespace RevendaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Acao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Detalhes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAuditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_DataHora",
                table: "RegistrosAuditoria",
                column: "DataHora",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_EmpresaId_DataHora",
                table: "RegistrosAuditoria",
                columns: new[] { "EmpresaId", "DataHora" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosAuditoria");
        }
    }
}