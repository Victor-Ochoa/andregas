using RevendaApp.Domain.Enums;

namespace RevendaApp.Web.Common;

/// <summary>
/// Rótulos de exibição pt-BR para os enums de domínio usados na interface. Centraliza os
/// switch de rótulo que antes existiam duplicados nos componentes Razor (ex.: 6 cópias de
/// "Gás do Povo") e garante que a UI nunca mostre o valor cru do enum ao operador.
/// Segue o mesmo padrão de <c>UsuarioPapelExtensions.Rotulo()</c>.
/// </summary>
public static class FormaPagamentoExtensions
{
    /// <summary>Rótulo amigável da forma de pagamento (ex.: "Gás do Povo").</summary>
    public static string Rotulo(this FormaPagamento forma) => forma switch
    {
        FormaPagamento.Pix => "Pix",
        FormaPagamento.Debito => "Débito",
        FormaPagamento.Credito => "Crédito",
        FormaPagamento.Dinheiro => "Dinheiro",
        FormaPagamento.Fiado => "Fiado",
        FormaPagamento.GasDoPovo => "Gás do Povo",
        _ => forma.ToString(),
    };
}

/// <summary>Rótulos de exibição pt-BR de <see cref="VendaStatus"/>.</summary>
public static class VendaStatusExtensions
{
    /// <summary>Rótulo amigável da situação da venda (ex.: "Fiado aberto").</summary>
    public static string Rotulo(this VendaStatus status) => status switch
    {
        VendaStatus.Pago => "Pago",
        VendaStatus.FiadoAberto => "Fiado aberto",
        VendaStatus.FiadoQuitado => "Fiado quitado",
        _ => status.ToString(),
    };
}

/// <summary>Rótulos de exibição pt-BR de <see cref="TipoProduto"/>.</summary>
public static class TipoProdutoExtensions
{
    /// <summary>Rótulo amigável do tipo de produto (ex.: "Gás" com acento).</summary>
    public static string Rotulo(this TipoProduto tipo) => tipo switch
    {
        TipoProduto.Gas => "Gás",
        TipoProduto.Agua => "Água",
        TipoProduto.Outros => "Outros",
        _ => tipo.ToString(),
    };
}

/// <summary>Rótulos de exibição pt-BR de <see cref="TipoHistoricoEstoque"/>.</summary>
public static class TipoHistoricoEstoqueExtensions
{
    /// <summary>Rótulo amigável do tipo de histórico de estoque (ex.: "Saída").</summary>
    public static string Rotulo(this TipoHistoricoEstoque tipo) => tipo switch
    {
        TipoHistoricoEstoque.Entrada => "Entrada",
        TipoHistoricoEstoque.Saida => "Saída",
        TipoHistoricoEstoque.Ajuste => "Ajuste",
        TipoHistoricoEstoque.Venda => "Venda",
        TipoHistoricoEstoque.Edicao => "Edição",
        TipoHistoricoEstoque.Cadastro => "Cadastro",
        TipoHistoricoEstoque.EmprestimoVasilhame => "Empréstimo de vasilhame",
        _ => tipo.ToString(),
    };
}

/// <summary>Rótulos de exibição pt-BR de <see cref="PeriodoDashboard"/>.</summary>
public static class PeriodoDashboardExtensions
{
    /// <summary>Rótulo amigável do período do dashboard (ex.: "Últimas 24 horas").</summary>
    public static string Rotulo(this PeriodoDashboard periodo) => periodo switch
    {
        PeriodoDashboard.UltimaHora => "Última hora",
        PeriodoDashboard.Ultimas3Horas => "Últimas 3 horas",
        PeriodoDashboard.Hoje => "Hoje",
        PeriodoDashboard.Ultimas24Horas => "Últimas 24 horas",
        PeriodoDashboard.Ultimos7Dias => "Últimos 7 dias",
        PeriodoDashboard.EsteMes => "Este mês",
        PeriodoDashboard.Tudo => "Tudo",
        _ => periodo.ToString(),
    };
}