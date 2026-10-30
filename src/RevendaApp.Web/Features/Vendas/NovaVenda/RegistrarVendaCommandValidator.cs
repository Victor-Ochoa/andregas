using FluentValidation;

using Microsoft.EntityFrameworkCore;

using RevendaApp.Domain.Entities;
using RevendaApp.Infrastructure;

namespace RevendaApp.Web.Features.Vendas.NovaVenda;

public sealed class RegistrarVendaCommandValidator : AbstractValidator<RegistrarVendaCommand>
{
    public RegistrarVendaCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.Itens)
            .NotEmpty().WithMessage("Adicione ao menos um produto à venda.");

        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantidade)
                .GreaterThan(0).WithMessage("A quantidade de cada item deve ser maior que zero.");

            // Preço unitário opcional; quando informado (preço personalizado), deve ser positivo.
            item.RuleFor(i => i.PrecoUnitario)
                .GreaterThan(0).WithMessage("O preço unitário do item deve ser maior que zero.")
                .When(i => i.PrecoUnitario is not null);
        });

        RuleFor(x => x.Desconto)
            .GreaterThanOrEqualTo(0).WithMessage("O desconto não pode ser negativo.");

        RuleFor(x => x.ValorEntrega)
            .GreaterThanOrEqualTo(0).WithMessage("O valor da entrega não pode ser negativo.");

        // Quando não há cliente selecionado, é um cadastro rápido: nome, telefone e endereço
        // são obrigatórios (cada um com erro próprio de campo).
        RuleFor(x => x.NomeClienteNovo)
            .NotEmpty().WithMessage("Informe o nome do novo cliente.")
            .When(x => x.ClienteId is null);

        RuleFor(x => x.TelefoneClienteNovo)
            .NotEmpty().WithMessage("Informe o telefone do novo cliente.")
            .When(x => x.ClienteId is null);

        RuleFor(x => x.EnderecoClienteNovo)
            .NotEmpty().WithMessage("Informe o endereço do novo cliente.")
            .When(x => x.ClienteId is null);

        // Quando um ClienteId é informado, ele deve existir.
        RuleFor(x => x.ClienteId)
            .MustAsync(async (Guid? clienteId, CancellationToken cancellationToken) =>
            {
                if (clienteId is not Guid id)
                {
                    return true;
                }

                return await db.Clientes.AnyAsync(c => c.Id == id, cancellationToken);
            })
            .WithMessage("O cliente selecionado não existe.");

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

                var emprestadosCliente = command.ClienteId is Guid clienteId
                    ? await db.EmprestimosVasilhames
                        .Where(e => e.ClienteId == clienteId && e.ProdutoId == produto.Id && e.DevolvidoEm == null)
                        .SumAsync(e => (int?)(e.Quantidade - e.QuantidadeDevolvida), cancellationToken) ?? 0
                    : 0;

                return produto.QuantidadeEstoque + emprestadosCliente >= item.Quantidade;
            }

            return produto.QuantidadeEstoque >= item.Quantidade;
        }).WithMessage("Um dos produtos está sem estoque suficiente, desabilitado ou não existe mais. Verifique os itens da venda.");
    }
}