using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

public sealed class RegistrarVendaCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<RegistrarVendaCommand, RegistrarVendaResult>
{
    public async ValueTask<RegistrarVendaResult> Handle(RegistrarVendaCommand command, CancellationToken cancellationToken)
    {
        var (usuario, vendedorId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        var resultado = await RegistrarAsync(db, command, vendedorId, usuario, cancellationToken);

        // Persiste a venda (itens, movimentações, pagamento e histórico).
        await db.SaveChangesAsync(cancellationToken);

        return resultado;
    }

    /// <summary>
    /// Registra uma venda no <paramref name="db"/> **sem persistir** (não chama
    /// <c>SaveChangesAsync</c>). Usado pelo fluxo de Nova Venda (que persiste em seguida) e pela
    /// edição de venda (que precisa re-criar a venda numa transação com a reversão dos efeitos
    /// antigos, sem salvar no meio). Centraliza as regras de negócio: preço por forma de
    /// pagamento, preço personalizado por cliente, Gás do Povo, desconto, estoque, histórico e
    /// pagamento de venda paga no ato.
    /// </summary>
    internal static async Task<RegistrarVendaResult> RegistrarAsync(
        AppDbContext db,
        RegistrarVendaCommand command,
        Guid? vendedorId,
        string usuario,
        CancellationToken cancellationToken)
    {
        // Cliente existente (selecionado no autocomplete) ou novo cadastro rápido.
        Cliente cliente;
        if (command.ClienteId is Guid clienteId)
        {
            cliente = await db.Clientes.FindAsync([clienteId], cancellationToken)
                ?? throw new InvalidOperationException("O cliente selecionado não existe mais.");
        }
        else
        {
            var telefoneNormalizado = Cliente.NormalizarTelefone(command.TelefoneClienteNovo!);
            var telefoneJaExiste = await db.Clientes.AnyAsync(c => c.Telefone == telefoneNormalizado, cancellationToken);
            if (telefoneJaExiste)
            {
                throw new InvalidOperationException("Já existe um cliente com este telefone. Busque-o pelo nome na tela de venda.");
            }

            cliente = new Cliente(command.NomeClienteNovo!, command.TelefoneClienteNovo!, command.EnderecoClienteNovo!);
            db.Clientes.Add(cliente);
        }

        var venda = new Venda(cliente.Id, command.FormaPagamento);
        venda.DefinirVendedor(vendedorId);

        foreach (var itemInput in command.Itens)
        {
            var produto = await db.Produtos.FindAsync([itemInput.ProdutoId], cancellationToken)
                ?? throw new InvalidOperationException($"Produto '{itemInput.ProdutoId}' não encontrado.");

            if (!produto.Ativo)
            {
                throw new InvalidOperationException($"O produto '{produto.Nome}' está desabilitado e não pode ser vendido.");
            }

            var precoBase = produto.PrecoParaFormaPagamento(command.FormaPagamento);

            // O preço personalizado (acordo cliente+produto) não se aplica a "Gás do Povo", que usa
            // sempre o preço subsidiado do produto.
            decimal precoUnitario;
            if (command.FormaPagamento == FormaPagamento.GasDoPovo)
            {
                precoUnitario = precoBase;
            }
            else if (itemInput.PrecoUnitario is decimal precoInformado)
            {
                // O preço exibido/confirmado na tela (personalizado do cliente ou editado pelo operador).
                precoUnitario = precoInformado;
                await AtualizarAcordoPrecoAsync(db, cliente.Id, produto.Id, precoInformado, precoBase, cancellationToken);
            }
            else
            {
                // Sem preço informado: usa o preço padrão (comportamento atual) e não mexe no acordo.
                precoUnitario = precoBase;
            }

            venda.AdicionarItem(produto.Id, itemInput.Quantidade, precoUnitario, produto.PrecoCusto);

            produto.RegistrarSaida(itemInput.Quantidade);
            db.MovimentacoesEstoque.Add(new MovimentacaoEstoque(produto.Id, TipoMovimentacaoEstoque.Saida, itemInput.Quantidade, "Venda"));
        }

        if (command.Desconto > 0)
        {
            venda.AplicarDesconto(command.Desconto);
        }

        venda.DefinirValorEntrega(command.ValorEntrega);

        // Só soma ao saldo devedor quando a venda é fiado; demais formas de pagamento são
        // liquidadas no ato e não alteram o saldo do cliente.
        if (command.FormaPagamento == FormaPagamento.Fiado)
        {
            cliente.AdicionarSaldoDevedor(venda.ValorTotal);
        }
        else
        {
            // Venda paga no ato: registra o pagamento recebido (não abate saldo devedor, pois
            // a venda não foi fiado). O pagamento fica vinculado à venda (VendaId) para permitir
            // estorno/edição quando a venda for excluída.
            db.Pagamentos.Add(new Pagamento(cliente.Id, venda.ValorTotal, command.FormaPagamento, "Venda", vendaId: venda.Id));
        }

        db.Vendas.Add(venda);

        // Histórico de estoque consolidado por produto da venda (soma as quantidades do mesmo
        // produto), evitando uma linha por item. A MovimentacaoEstoque acima mantém 1 linha/item.
        foreach (var grupo in command.Itens.GroupBy(i => i.ProdutoId))
        {
            var quantidade = grupo.Sum(i => i.Quantidade);
            var palavra = quantidade == 1 ? "produto" : "produtos";
            db.HistoricosEstoque.Add(new HistoricoEstoque(
                grupo.Key,
                TipoHistoricoEstoque.Venda,
                $"Venda de {quantidade} {palavra}",
                "Venda",
                usuario,
                quantidade: quantidade,
                vendaId: venda.Id));
        }

        return new RegistrarVendaResult(venda.Id, cliente.Id, venda.ValorTotal, venda.LucroTotal);
    }

    /// <summary>
    /// Sincroniza o acordo de preço personalizado entre cliente e produto: cria/atualiza quando o
    /// preço praticado difere do padrão; remove quando volta ao padrão. Não lança quando o preço é
    /// inválido — a validação (FluentValidation) já impede preços &lt;= 0.
    /// </summary>
    private static async Task AtualizarAcordoPrecoAsync(
        AppDbContext db, Guid clienteId, Guid produtoId, decimal precoPraticado, decimal precoPadrao, CancellationToken cancellationToken)
    {
        var acordo = await db.PrecosPersonalizadosClientes
            .FirstOrDefaultAsync(p => p.ClienteId == clienteId && p.ProdutoId == produtoId, cancellationToken);

        if (precoPraticado == precoPadrao)
        {
            // Voltou ao preço padrão do produto: o acordo deixa de existir.
            if (acordo is not null)
            {
                db.PrecosPersonalizadosClientes.Remove(acordo);
            }

            return;
        }

        // Preço prático diferente do padrão (acordo negociado).
        if (acordo is null)
        {
            db.PrecosPersonalizadosClientes.Add(new PrecoPersonalizadoCliente(clienteId, produtoId, precoPraticado));
        }
        else
        {
            acordo.AtualizarPreco(precoPraticado);
        }
    }
}