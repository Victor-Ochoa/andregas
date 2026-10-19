using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGas.Web.Features.Estoque.Cadastrar;

public sealed class CadastrarProdutoCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null,
    IUsuarioAutenticado? usuarioAutenticado = null)
    : ICommandHandler<CadastrarProdutoCommand, Guid>
{
    public async ValueTask<Guid> Handle(CadastrarProdutoCommand command, CancellationToken cancellationToken)
    {
        // Operação restrita a administradores. Em produção o DI sempre resolve o serviço scoped;
        // null significa que quem chamou construiu o handler manualmente (testes) e não exigiu a guarda.
        if (usuarioAutenticado is not null)
        {
            await usuarioAutenticado.RequerAdminAsync(cancellationToken);
        }

        var produto = new Produto(
            command.Nome,
            command.Tipo,
            command.PrecoVenda,
            command.PrecoCusto,
            command.PrecoGasDoPovo,
            command.EstoqueMinimo,
            command.ControlaVasilhame);

        db.Produtos.Add(produto);

        // Entrada inicial de vasilhames vazios no depósito quando informada no cadastro.
        if (command.QuantidadeVasilhamesInicial > 0)
        {
            produto.RegistrarEntradaVasilhame(command.QuantidadeVasilhamesInicial);
        }

        await db.SaveChangesAsync(cancellationToken);

        var (usuario, _) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
        var descricaoCadastro = command.QuantidadeVasilhamesInicial > 0
            ? $"Cadastro inicial com entrada de {command.QuantidadeVasilhamesInicial} vasilhame{(command.QuantidadeVasilhamesInicial == 1 ? string.Empty : "s")} vazio(s)"
            : "Cadastro inicial";
        db.HistoricosEstoque.Add(new HistoricoEstoque(
            produto.Id,
            TipoHistoricoEstoque.Cadastro,
            descricaoCadastro,
            descricaoCadastro,
            usuario));

        // Entrada de vazios também gera histórico dedicado de vasilhame quando > 0, para a
        // modal Histórico detalhar o movimento (padrão dos demais fluxos de vazios).
        if (command.QuantidadeVasilhamesInicial > 0)
        {
            db.HistoricosEstoque.Add(new HistoricoEstoque(
                produto.Id,
                TipoHistoricoEstoque.Vasilhame,
                command.QuantidadeVasilhamesInicial == 1
                    ? "Entrada de 1 vasilhame vazio ao depósito (cadastro)"
                    : $"Entrada de {command.QuantidadeVasilhamesInicial} vasilhames vazios ao depósito (cadastro)",
                $"Estoque inicial de vasilhames",
                usuario,
                quantidade: command.QuantidadeVasilhamesInicial));
        }

        await db.SaveChangesAsync(cancellationToken);

        return produto.Id;
    }
}