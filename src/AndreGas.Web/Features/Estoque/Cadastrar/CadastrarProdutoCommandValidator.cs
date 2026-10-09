using FluentValidation;

namespace AndreGas.Web.Features.Estoque.Cadastrar;

public sealed class CadastrarProdutoCommandValidator : AbstractValidator<CadastrarProdutoCommand>
{
    public CadastrarProdutoCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do produto.")
            .MaximumLength(200);

        RuleFor(x => x.Tipo).IsInEnum();

        RuleFor(x => x.PrecoVenda)
            .GreaterThanOrEqualTo(0).WithMessage("O preço de venda não pode ser negativo.");

        RuleFor(x => x.PrecoCusto)
            .GreaterThanOrEqualTo(0).WithMessage("O preço de custo não pode ser negativo.");

        RuleFor(x => x.PrecoGasDoPovo)
            .GreaterThanOrEqualTo(0).WithMessage("O preço do Gás do Povo não pode ser negativo.");

        RuleFor(x => x.EstoqueMinimo)
            .GreaterThanOrEqualTo(0).WithMessage("O estoque mínimo não pode ser negativo.");
    }
}