using FluentValidation;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Infrastructure;

namespace RevendaApp.Web.Features.Admin.Vendas;

/// <summary>Valida o comando de edição de venda: existência da venda, itens não vazios,
/// quantidades positivas, desconto/entrega não negativos e estoque suficiente.</summary>
public sealed class EditarVendaCommandValidator : AbstractValidator<EditarVendaCommand>
{
    public EditarVendaCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.VendaId)
            .NotEmpty();

        RuleFor(x => x.VendaId)
            .MustAsync(async (id, cancellationToken) => await db.Vendas.AnyAsync(v => v.Id == id, cancellationToken))
            .WithMessage("A venda a editar não foi encontrada.");

        RuleFor(x => x.Itens)
            .NotEmpty().WithMessage("Adicione ao menos um produto à venda.");

        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantidade)
                .GreaterThan(0).WithMessage("A quantidade de cada item deve ser maior que zero.");

            item.RuleFor(i => i.PrecoUnitario)
                .GreaterThan(0).WithMessage("O preço unitário do item deve ser maior que zero.")
                .When(i => i.PrecoUnitario is not null);
        });

        RuleFor(x => x.Desconto)
            .GreaterThanOrEqualTo(0).WithMessage("O desconto não pode ser negativo.");

        RuleFor(x => x.ValorEntrega)
            .GreaterThanOrEqualTo(0).WithMessage("O valor da entrega não pode ser negativo.");

        // Cada produto deve existir, estar ativo e ter estoque suficiente para a quantidade pedida.
        // Empréstimo de vasilhame (item.EhEmprestimo) só é válido em produto que controla vasilhame
        // e permite o cliente "pegar emprestado" o que já está com ele: a quantidade máxima passa a
        // ser Estoque + Emprestados em aberto do cliente para aquele produto (cobre o caso em que o
        // estoque cheio está abaixo do pedido). Troca (e produtos sem controle) mantêm a regra atual.
        RuleForEach(x => x.Itens).MustAsync(async (command, item, _, cancellationToken) =>
        {
            var produto = await db.Produtos.FindAsync([item.ProdutoId], cancellationToken);
            if (produto is not { Ativo: true })
            {
                return false;
            }

            if (item.EhEmprestimo)
            {
                if (!produto.ControlaVasilhame)
                {
                    return false;
                }

                var clienteId = await db.Vendas
                    .Where(v => v.Id == command.VendaId)
                    .Select(v => (Guid?)v.ClienteId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (clienteId is not Guid id)
                {
                    return false;
                }

                var emprestadosCliente = await db.EmprestimosVasilhames
                    .Where(e => e.ClienteId == id
                        && e.ProdutoId == produto.Id
                        && e.DevolvidoEm == null)
                    .SumAsync(e => (int?)(e.Quantidade - e.QuantidadeDevolvida), cancellationToken) ?? 0;

                return produto.QuantidadeEstoque + emprestadosCliente >= item.Quantidade;
            }

            return produto.QuantidadeEstoque >= item.Quantidade;
        }).WithMessage("Estoque insuficiente, produto desabilitado ou não encontrado para um dos itens.");
    }
}