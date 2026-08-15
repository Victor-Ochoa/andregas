using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

public sealed class RegistrarVendaCommandValidator : AbstractValidator<RegistrarVendaCommand>
{
    public RegistrarVendaCommandValidator(AppDbContext db)
    {
        RuleFor(x => x.Telefone)
            .NotEmpty().WithMessage("Informe o telefone do cliente.");

        RuleFor(x => x.Itens)
            .NotEmpty().WithMessage("Adicione ao menos um produto à venda.");

        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantidade)
                .GreaterThan(0).WithMessage("A quantidade de cada item deve ser maior que zero.");
        });

        RuleFor(x => x.Desconto)
            .GreaterThanOrEqualTo(0).WithMessage("O desconto não pode ser negativo.");

        // Se o cliente ainda não existe (telefone novo), nome e endereço são obrigatórios.
        RuleFor(x => x.NomeClienteNovo)
            .NotEmpty().WithMessage("Informe o nome do cliente.")
            .WhenAsync(async (command, cancellationToken) => !await ClienteExisteAsync(db, command.Telefone, cancellationToken));

        RuleFor(x => x.EnderecoClienteNovo)
            .NotEmpty().WithMessage("Informe o endereço do cliente.")
            .WhenAsync(async (command, cancellationToken) => !await ClienteExisteAsync(db, command.Telefone, cancellationToken));

        // Cada produto deve existir e ter estoque suficiente para a quantidade pedida.
        RuleForEach(x => x.Itens).MustAsync(async (command, item, _, cancellationToken) =>
        {
            var produto = await db.Produtos.FindAsync([item.ProdutoId], cancellationToken);
            return produto is not null && produto.QuantidadeEstoque >= item.Quantidade;
        }).WithMessage("Estoque insuficiente ou produto não encontrado para um dos itens.");
    }

    private static async Task<bool> ClienteExisteAsync(AppDbContext db, string telefone, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(telefone))
        {
            return false;
        }

        var telefoneNormalizado = Cliente.NormalizarTelefone(telefone);
        return await db.Clientes.AnyAsync(c => c.Telefone == telefoneNormalizado, cancellationToken);
    }
}
