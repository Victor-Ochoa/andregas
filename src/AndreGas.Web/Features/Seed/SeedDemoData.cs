using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Seed;

/// <summary>
/// Popula o banco com dados de demonstração (clientes, produtos, vendas, pagamentos e
/// movimentações de estoque) com datas anteriores a hoje, cobrindo todas as formas de
/// pagamento — útil para exercitar os dashboards e fluxos do sistema.
/// </summary>
public static class SeedDemoData
{
    public static async Task SeedAsync(AppDbContext db, TimeProvider? timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        // Idempotente: se já existirem produtos (seed já aplicado), não duplica.
        if (await db.Produtos.AnyAsync())
        {
            return;
        }

        // ---- Produtos ----
        var botijao13 = new Produto("Botijão Gás 13kg", TipoProduto.Gas, 120m, 80m, 100m, estoqueMinimo: 10);
        var botijao45 = new Produto("Botijão Gás 45kg", TipoProduto.Gas, 350m, 260m, 300m, estoqueMinimo: 2);
        var agua20l = new Produto("Galão Água 20L", TipoProduto.Agua, 20m, 10m, 15m, estoqueMinimo: 20);
        var copoAgua = new Produto("Copo Água 500ml", TipoProduto.Outros, 2m, 1m, 1.5m, estoqueMinimo: 50);

        botijao13.RegistrarEntrada(50);
        botijao45.RegistrarEntrada(10);
        agua20l.RegistrarEntrada(80);
        copoAgua.RegistrarEntrada(200);

        db.Produtos.AddRange(botijao13, botijao45, agua20l, copoAgua);
        db.MovimentacoesEstoque.AddRange(
            new MovimentacaoEstoque(botijao13.Id, TipoMovimentacaoEstoque.Entrada, 50, "Seed inicial"),
            new MovimentacaoEstoque(botijao45.Id, TipoMovimentacaoEstoque.Entrada, 10, "Seed inicial"),
            new MovimentacaoEstoque(agua20l.Id, TipoMovimentacaoEstoque.Entrada, 80, "Seed inicial"),
            new MovimentacaoEstoque(copoAgua.Id, TipoMovimentacaoEstoque.Entrada, 200, "Seed inicial"));

        // ---- Clientes ----
        var maria = new Cliente("Maria Souza", "11911112222", "Rua das Flores, 100");
        var joao = new Cliente("João Pereira", "11933334444", "Av. Brasil, 250");
        var ana = new Cliente("Ana Lima", "11955556666", "Rua do Comércio, 15");
        var carlos = new Cliente("Carlos Nunes", "11977778888", "Travessa Bela, 8");
        var fatima = new Cliente("Fátima Rocha", "11999990000", "Rua Central, 32");

        db.Clientes.AddRange(maria, joao, ana, carlos, fatima);

        // ---- Vendas / Pagamentos / Estoque ----
        // Venda 1 — Pix (2 meses atrás) — entrega de 10,00.
        RegistrarVenda(db, maria, FormaPagamento.Pix, botijao13, 2, agua20l, 3, agora.AddMonths(-2).AddHours(-3), desconto: 10, valorEntrega: 10);

        // Venda 2 — Débito (1 mês atrás)
        RegistrarVenda(db, joao, FormaPagamento.Debito, botijao13, 1, copoAgua, 5, agora.AddMonths(-1).AddDays(-2));

        // Venda 3 — Crédito (25 dias atrás)
        RegistrarVenda(db, ana, FormaPagamento.Credito, botijao45, 1, agua20l, 4, agora.AddDays(-25));

        // Venda 4 — Dinheiro (10 dias atrás)
        RegistrarVenda(db, carlos, FormaPagamento.Dinheiro, botijao13, 3, copoAgua, 10, agora.AddDays(-10), desconto: 15);

        // Venda 5 — Gás do Povo (5 dias atrás) — usa PrecoGasDoPovo
        RegistrarVenda(db, fatima, FormaPagamento.GasDoPovo, botijao13, 2, agua20l, 2, agora.AddDays(-5));

        // Venda 6 — Fiado (8 dias atrás) e já quitado em seguida (pago 7 dias atrás) — entrega de 10,00.
        var vendaFiadoQuitado = RegistrarVenda(db, maria, FormaPagamento.Fiado, botijao13, 2, agua20l, 2, agora.AddDays(-8), valorEntrega: 10);
        // Pagamento que quita o fiado acima (FIFO): marca a venda como quitada e abate o saldo.
        vendaFiadoQuitado.MarcarFiadoQuitado();
        db.Pagamentos.Add(new Pagamento(maria.Id, vendaFiadoQuitado.ValorTotal, FormaPagamento.Dinheiro, "Quitação fiado", agora.AddDays(-7)));
        maria.RegistrarPagamento(vendaFiadoQuitado.ValorTotal);

        // Venda 7 — Fiado ainda em aberto (hoje - 3 dias) → mantém saldo devedor.
        RegistrarVenda(db, joao, FormaPagamento.Fiado, botijao13, 1, data: agora.AddDays(-3));

        // Venda 8 — Fiado em aberto mais recente (hoje - 1 dia) → soma mais saldo.
        RegistrarVenda(db, ana, FormaPagamento.Fiado, botijao45, 1, data: agora.AddDays(-1));

        // Venda 9 — Pix de hoje (para alimentar o período "Hoje") — entrega de 5,00.
        RegistrarVenda(db, carlos, FormaPagamento.Pix, agua20l, 5, copoAgua, 8, agora.AddHours(-2), desconto: 5, valorEntrega: 5);

        await db.SaveChangesAsync();
    }

    private static Venda RegistrarVenda(
        AppDbContext db,
        Cliente cliente,
        FormaPagamento formaPagamento,
        Produto produto1,
        int qtd1,
        Produto? produto2 = null,
        int qtd2 = 0,
        DateTime? data = null,
        decimal desconto = 0,
        decimal valorEntrega = 0)
    {
        var venda = new Venda(cliente.Id, formaPagamento, data);

        var preco1 = produto1.PrecoParaFormaPagamento(formaPagamento);
        venda.AdicionarItem(produto1.Id, qtd1, preco1, produto1.PrecoCusto);
        produto1.RegistrarSaida(qtd1);
        db.MovimentacoesEstoque.Add(new MovimentacaoEstoque(produto1.Id, TipoMovimentacaoEstoque.Saida, qtd1, "Venda"));

        if (produto2 is not null && qtd2 > 0)
        {
            var preco2 = produto2.PrecoParaFormaPagamento(formaPagamento);
            venda.AdicionarItem(produto2.Id, qtd2, preco2, produto2.PrecoCusto);
            produto2.RegistrarSaida(qtd2);
            db.MovimentacoesEstoque.Add(new MovimentacaoEstoque(produto2.Id, TipoMovimentacaoEstoque.Saida, qtd2, "Venda"));
        }

        if (desconto > 0)
        {
            venda.AplicarDesconto(desconto);
        }

        if (valorEntrega > 0)
        {
            venda.DefinirValorEntrega(valorEntrega);
        }

        if (formaPagamento == FormaPagamento.Fiado)
        {
            cliente.AdicionarSaldoDevedor(venda.ValorTotal);
        }
        else
        {
            // Pagamento registrado na mesma data/hora da venda (não em agora), para que o
            // pagamento "bata" com a venda e os dashboards por período façam sentido.
            db.Pagamentos.Add(new Pagamento(cliente.Id, venda.ValorTotal, formaPagamento, "Venda", venda.DataHora));
        }

        db.Vendas.Add(venda);
        return venda;
    }
}