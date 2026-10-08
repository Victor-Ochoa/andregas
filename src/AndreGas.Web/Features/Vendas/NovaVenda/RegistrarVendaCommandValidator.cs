using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Vendas.NovaVenda;

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
        RuleForEach(x => x.Itens).MustAsync(async (command, item, _, cancellationToken) =>
        {
            var produto = await db.Produtos.FindAsync([item.ProdutoId], cancellationToken);
            return produto is { Ativo: true } && produto.QuantidadeEstoque >= item.Quantidade;
        }).WithMessage("Estoque insuficiente, produto desabilitado ou não encontrado para um dos itens.");
    }
}
