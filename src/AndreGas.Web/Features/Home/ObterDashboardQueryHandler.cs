using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Home;

public sealed class ObterDashboardQueryHandler(AppDbContext db, TimeProvider timeProvider) : IQueryHandler<ObterDashboardQuery, DashboardResult>
{
    public async ValueTask<DashboardResult> Handle(ObterDashboardQuery query, CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var inicio = ObterInicioPeriodo(query.Periodo, agora);

        // ValorTotal/LucroTotal/Desconto são calculados em memória a partir dos Itens (não são
        // colunas mapeadas), então é preciso carregar as vendas com os itens antes de agregar.
        var vendas = await db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Cliente)
            // Vendas excluídas (soft delete) ficam no banco, mas não contam no dashboard.
            .Where(v => v.ExcluidaEm == null
                        && (query.Periodo == PeriodoDashboard.Tudo || v.DataHora >= inicio))
            .ToListAsync(cancellationToken);

        // Venda fiado é contabilizada como venda, mas seu lucro só é reconhecido quando o
        // saldo devedor é pago. Por isso o lucro de vendas fiado ainda em aberto é excluído dos
        // lucros; vendas fiado já quitadas (Status == FiadoQuitado) entram no lucro (o total de
        // vendas sempre conta com o fiado).
        var lucroReconhecido = (IEnumerable<Venda> vendas) =>
            vendas
                .Where(v => v.FormaPagamento != FormaPagamento.Fiado || v.Status == VendaStatus.FiadoQuitado)
                .Sum(v => v.LucroTotal);

        var totalDevedor = await db.Clientes.SumAsync(c => c.SaldoDevedor, cancellationToken);

        var pagamentos = await db.Pagamentos
            .Include(p => p.Cliente)
            .Where(p => query.Periodo == PeriodoDashboard.Tudo || p.Data >= inicio)
            .OrderByDescending(p => p.Data)
            .ToListAsync(cancellationToken);

        var produtosEstoqueBaixo = await db.Produtos
            .Where(p => p.Ativo && p.QuantidadeEstoque <= p.EstoqueMinimo)
            .OrderBy(p => p.Nome)
            .Select(p => new ProdutoEstoqueBaixoItem(p.Id, p.Nome, p.QuantidadeEstoque, p.EstoqueMinimo))
            .ToListAsync(cancellationToken);

        // Estado físico de TODOS os produtos para o gráfico da Home (inclui produtos sem controle
        // e inativos). Categorias: Estoque (cheios), Vasilhame (vazios disponíveis) e Emprestado
        // (emprestados a clientes). Produtos sem controle ficam com Vasilhame/Emprestado zerados.
        var vasilhames = await db.Produtos
            .OrderBy(p => p.Nome)
            .Select(p => new VasilhameDashboardItem(
                p.Id,
                p.Nome,
                p.QuantidadeEstoque,
                p.ControlaVasilhame ? p.QuantidadeVasilhames : 0,
                p.ControlaVasilhame ? p.QuantidadeVasilhamesEmprestados : 0))
            .ToListAsync(cancellationToken);

        // Total de vendas por forma de pagamento no período (para o donut).
        var vendasPorForma = vendas
            .GroupBy(v => v.FormaPagamento)
            .OrderByDescending(g => g.Sum(v => v.ValorTotal))
            .Select(g => new VendaPorFormaItem(g.Key, g.Sum(v => v.ValorTotal)))
            .ToList();

        // Produtos mais vendidos no período (por quantidade), com receita e lucro líquido.
        var topProdutos = vendas
            .SelectMany(v => v.Itens)
            .GroupBy(i => new ProdutoKey(i.ProdutoId, i.Produto?.Nome ?? "Produto removido"))
            .OrderByDescending(g => g.Sum(i => i.Quantidade))
            .Take(10)
            .Select(g => new TopProdutoItem(
                g.Key.ProdutoId,
                g.Key.Nome,
                g.Sum(i => i.Quantidade),
                g.Sum(i => i.PrecoUnitario * i.Quantidade),
                g.Sum(i => i.Lucro)))
            .ToList();

        // Clientes com maior saldo devedor em aberto, independentemente do período.
        var topDevedores = await db.Clientes
            .Where(c => c.SaldoDevedor > 0)
            .OrderByDescending(c => c.SaldoDevedor)
            .Take(10)
            .Select(c => new TopDevedorItem(c.Id, c.Nome, c.SaldoDevedor))
            .ToListAsync(cancellationToken);

        // Avisos de recompra: clientes que já passaram da data prevista de nova compra de produtos
        // com AvisarRecompra (resumo aprendido em RecorrenciaCompra, independente do período).
        // A decisão de "hoje" usa o dia civil de Brasília (o aviso começa na virada local, não na
        // meia-noite UTC): converte a última compra e o "agora" para o fuso do negócio e compara em
        // dias de calendário. Janela: mostra até dataPrevista + 2×intervalo (fator 3× a partir da
        // última compra); quem não recompra em mais de 2 ciclos sai da lista. Top 10, mais atrasado primeiro.
        var hojeBrasilia = BrasilTimeZone.ParaBrasilia(timeProvider.GetUtcNow().UtcDateTime).Date;
        var recompras = await db.RecorrenciasCompra
            .Where(r => r.IntervaloMedioDias > 0
                && r.Produto != null && r.Produto.AvisarRecompra)
            .Include(r => r.Cliente)
            .Include(r => r.Produto)
            .ToListAsync(cancellationToken);

        var listaRecompras = recompras
            .Where(r => r.Produto is not null && r.Cliente is not null)
            .Select(r => new
            {
                r,
                UltimaCompraBrasilia = BrasilTimeZone.ParaBrasilia(r.DataUltimaCompra).Date,
                IntervaloDias = (double)r.IntervaloMedioDias,
            })
            .Where(x => x.UltimaCompraBrasilia.AddDays(x.IntervaloDias) <= hojeBrasilia
                && hojeBrasilia <= x.UltimaCompraBrasilia.AddDays(x.IntervaloDias * 3))
            .OrderBy(x => x.UltimaCompraBrasilia.AddDays(x.IntervaloDias))
            .Take(10)
            .Select(x => new RecompraItem(
                x.r.Cliente!.Id,
                x.r.Cliente!.Nome,
                x.r.Cliente!.Telefone,
                x.r.Produto!.Id,
                x.r.Produto!.Nome,
                BrasilTimeZone.ParaBrasilia(x.r.DataUltimaCompra),
                x.r.IntervaloMedioDias,
                x.UltimaCompraBrasilia.AddDays(x.IntervaloDias),
                (int)(hojeBrasilia - x.UltimaCompraBrasilia).TotalDays,
                x.r.QuantidadeCompras))
            .ToList();

        return new DashboardResult(
            Periodo: query.Periodo,
            VendasTotal: vendas.Sum(v => v.ValorTotal),
            VendasQuantidade: vendas.Count,
            LucroTotal: lucroReconhecido(vendas),
            DescontoTotal: vendas.Sum(v => v.Desconto),
            TotalDevedor: totalDevedor,
            VendasPorPeriodo: AgruparPorPeriodo(vendas, query.Periodo),
            ProdutosEstoqueBaixo: produtosEstoqueBaixo,
            Vendas: vendas
                .OrderByDescending(v => v.DataHora)
                .Select(v => new VendaDetalhe(
                    v.Id,
                    BrasilTimeZone.ParaBrasilia(v.DataHora),
                    v.Cliente?.Nome ?? string.Empty,
                    v.Cliente?.Telefone ?? string.Empty,
                    v.FormaPagamento,
                    v.Status,
                    v.Itens.Sum(i => i.Quantidade),
                    v.ValorTotal,
                    v.LucroTotal,
                    v.Desconto,
                    v.ValorEntrega,
                    v.Itens.Select(i => i.ProdutoId).ToArray()))
                .ToList(),
            Pagamentos: pagamentos
                .Select(p => new PagamentoDetalhe(
                    BrasilTimeZone.ParaBrasilia(p.Data),
                    p.Cliente?.Nome ?? string.Empty,
                    p.Cliente?.Telefone ?? string.Empty,
                    p.FormaPagamento,
                    p.Valor,
                    p.Observacao))
                .ToList(),
            VendasPorForma: vendasPorForma,
            TopProdutos: topProdutos,
            TopDevedores: topDevedores,
            Vasilhames: vasilhames,
            Recompras: listaRecompras);
    }

    private sealed record ProdutoKey(Guid ProdutoId, string Nome);

    private static DateTime ObterInicioPeriodo(PeriodoDashboard periodo, DateTime agora)
    {
        // "agora" é UTC; convertemos para o fuso de Brasília apenas para delimitar o calendário
        // ("hoje"/"este mês"). Janelas de duração (UltimaHora, Ultimas3Horas, Ultimas24Horas) são
        // físicas (rolam com o tempo) — o fuso não as altera.
        var hojeLocal = BrasilTimeZone.ParaBrasilia(agora).Date;

        static DateTime InicioLocal(DateTime local)
        {
            // Converte uma meia-noite local de volta para UTC para comparar com as colunas timestamptz.
            return BrasilTimeZone.ParaUtc(local);
        }

        return periodo switch
        {
            PeriodoDashboard.UltimaHora => agora.AddHours(-1),
            PeriodoDashboard.Ultimas3Horas => agora.AddHours(-3),
            PeriodoDashboard.Hoje => InicioLocal(hojeLocal),
            PeriodoDashboard.Ultimas24Horas => agora.AddHours(-24),
            PeriodoDashboard.Ultimos7Dias => InicioLocal(hojeLocal.AddDays(-6)),
            PeriodoDashboard.EsteMes => InicioLocal(new DateTime(hojeLocal.Year, hojeLocal.Month, 1)),
            PeriodoDashboard.Tudo => DateTime.MinValue,
            _ => InicioLocal(hojeLocal),
        };
    }

    private static List<VendaPeriodo> AgruparPorPeriodo(
        IReadOnlyList<Venda> vendas,
        PeriodoDashboard periodo)
    {
        // Janelas de até 24h (incluindo hoje) agrupam por hora; períodos maiores agrupam por dia.
        var porHora = periodo is PeriodoDashboard.UltimaHora
            or PeriodoDashboard.Ultimas3Horas
            or PeriodoDashboard.Ultimas24Horas
            or PeriodoDashboard.Hoje;

        // O lucro de vendas fiado ainda em aberto não é reconhecido no gráfico.
        static decimal LucroReconhecido(IEnumerable<Venda> vs) =>
            vs
                .Where(v => v.FormaPagamento != FormaPagamento.Fiado || v.Status == VendaStatus.FiadoQuitado)
                .Sum(v => v.LucroTotal);

        if (porHora)
        {
            return vendas
                .GroupBy(v => BrasilTimeZone.ParaBrasilia(v.DataHora).Hour)
                .OrderBy(g => g.Key)
                .Select(g => new VendaPeriodo($"{g.Key:D2}h", g.Sum(v => v.ValorTotal), LucroReconhecido(g)))
                .ToList();
        }

        return vendas
            .GroupBy(v => DateOnly.FromDateTime(BrasilTimeZone.ParaBrasilia(v.DataHora)))
            .OrderBy(g => g.Key)
            .Select(g => new VendaPeriodo($"{g.Key:dd/MM}", g.Sum(v => v.ValorTotal), LucroReconhecido(g)))
            .ToList();
    }
}