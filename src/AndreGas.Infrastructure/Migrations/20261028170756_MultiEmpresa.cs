using System;

using Microsoft.EntityFrameworkCore.Migrations;

// CA1861: o gerador do EF Core emite "new[]{...}" inline para o índice composto; suprimir no
// arquivo gerado (o código de migração não é editado manualmente).
#pragma warning disable CA1861

#nullable disable

namespace AndreGas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clientes_Telefone",
                table: "Clientes");

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Vendas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "RecorrenciasCompra",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Produtos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "PrecosPersonalizadosClientes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Pagamentos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "MovimentacoesEstoque",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "ItensVenda",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "HistoricosEstoque",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "EmprestimosVasilhames",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Clientes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identificador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Endereco = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Cep = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Telefones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CorPrimaria = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    Logo = table.Column<string>(type: "character varying(700000)", maxLength: 700000, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_EmpresaId",
                table: "Vendas",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_RecorrenciasCompra_EmpresaId",
                table: "RecorrenciasCompra",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_EmpresaId",
                table: "Produtos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_PrecosPersonalizadosClientes_EmpresaId",
                table: "PrecosPersonalizadosClientes",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_EmpresaId",
                table: "Pagamentos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoque_EmpresaId",
                table: "MovimentacoesEstoque",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensVenda_EmpresaId",
                table: "ItensVenda",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosEstoque_EmpresaId",
                table: "HistoricosEstoque",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_EmprestimosVasilhames_EmpresaId",
                table: "EmprestimosVasilhames",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId",
                table: "Clientes",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Telefone",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Telefone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_EmpresaId",
                table: "AspNetUsers",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Identificador",
                table: "Empresas",
                column: "Identificador",
                unique: true);

            // Os defaults Guid.Empty acima existem só para permitir o ADD COLUMN NOT NULL em um
            // banco já populado (volume de dev/testes). Removidos aqui para o schema final não
            // aceitar silenciosamente uma linha de domínio sem empresa (tenant Guid.Empty).
            migrationBuilder.Sql("ALTER TABLE \"Vendas\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"RecorrenciasCompra\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Produtos\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"PrecosPersonalizadosClientes\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Pagamentos\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"MovimentacoesEstoque\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"ItensVenda\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"HistoricosEstoque\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"EmprestimosVasilhames\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Clientes\" ALTER COLUMN \"EmpresaId\" DROP DEFAULT;");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Empresas_EmpresaId",
                table: "AspNetUsers",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ATENÇÃO — revert da migração MultiEmpresa. Este Down() desfaz o modelo multi-empresa
            // e por isso tem duas perdas irreversíveis embutidas:
            //
            // 1. As atribuições de empresa são DESCARTADAS. O DropColumn("EmpresaId") nas 10
            //    tabelas de domínio apaga de vez qual empresa era dona de cada cliente, produto,
            //    venda etc. Não há como reconstruir essa informação depois do revert.
            //
            // 2. O índice único GLOBAL "IX_Clientes_Telefone" é recriado. Ele só é válido
            //    enquanto NENHUMA duas empresas tiverem o mesmo telefone cadastrado — exatamente
            //    o cenário que esta migração passou a permitir. Se houver telefone duplicado
            //    entre empresas, o revert é impossível sem antes deduplicar manualmente.
            //
            // O bloco SQL abaixo verifica a condição (2) e aborta com mensagem explícita ANTES de
            // qualquer operação destrutiva, em vez de estourar no meio do CREATE INDEX com um
            // erro genérico de constraint. Como o EF executa a migração numa transação, o abort
            // desfaz tudo — o banco continua no estado multi-empresa, íntegro.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    telefones_duplicados bigint;
                BEGIN
                    SELECT count(*) INTO telefones_duplicados
                    FROM (
                        SELECT "Telefone"
                        FROM "Clientes"
                        GROUP BY "Telefone"
                        HAVING count(*) > 1
                    ) AS duplicados;

                    IF telefones_duplicados > 0 THEN
                        RAISE EXCEPTION
                            'Revert da migracao MultiEmpresa abortado: % telefone(s) estao duplicados entre empresas na tabela "Clientes". O indice unico global IX_Clientes_Telefone nao pode ser recriado. Deduplicate manualmente (ou mantenha a migracao aplicada) antes de reverter. Nenhuma alteracao foi aplicada.',
                            telefones_duplicados;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Empresas_EmpresaId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropIndex(
                name: "IX_Vendas_EmpresaId",
                table: "Vendas");

            migrationBuilder.DropIndex(
                name: "IX_RecorrenciasCompra_EmpresaId",
                table: "RecorrenciasCompra");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_EmpresaId",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_PrecosPersonalizadosClientes_EmpresaId",
                table: "PrecosPersonalizadosClientes");

            migrationBuilder.DropIndex(
                name: "IX_Pagamentos_EmpresaId",
                table: "Pagamentos");

            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesEstoque_EmpresaId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropIndex(
                name: "IX_ItensVenda_EmpresaId",
                table: "ItensVenda");

            migrationBuilder.DropIndex(
                name: "IX_HistoricosEstoque_EmpresaId",
                table: "HistoricosEstoque");

            migrationBuilder.DropIndex(
                name: "IX_EmprestimosVasilhames_EmpresaId",
                table: "EmprestimosVasilhames");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EmpresaId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EmpresaId_Telefone",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_EmpresaId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "RecorrenciasCompra");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "PrecosPersonalizadosClientes");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "ItensVenda");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "HistoricosEstoque");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "EmprestimosVasilhames");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Telefone",
                table: "Clientes",
                column: "Telefone",
                unique: true);
        }
    }
}