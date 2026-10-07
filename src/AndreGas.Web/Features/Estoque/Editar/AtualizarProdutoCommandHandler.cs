using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using Mediator;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.Estoque.Editar;

public sealed class AtualizarProdutoCommandHandler(AppDbContext db, AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<AtualizarProdutoCommand, bool>
{
    public async ValueTask<bool> Handle(AtualizarProdutoCommand command, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos.FindAsync([command.ProdutoId], cancellationToken);
        if (produto is null)
        {
            return false;
        }

        var mudancas = CapturarMudancas(produto, command);

        produto.AtualizarDados(command.Nome, command.Tipo, command.PrecoVenda, command.PrecoCusto, command.PrecoGasDoPovo, command.EstoqueMinimo);

        if (command.Ativo)
        {
            produto.Ativar();
        }
        else
        {
            produto.Desativar();
        }

        await db.SaveChangesAsync(cancellationToken);

        if (mudancas.Count > 0)
        {
            var (usuario, _) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);
            db.HistoricosEstoque.Add(new HistoricoEstoque(
                command.ProdutoId,
                TipoHistoricoEstoque.Edicao,
                string.Join("\n", mudancas),
                "Edição de dados",
                usuario));
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static List<string> CapturarMudancas(Produto produto, AtualizarProdutoCommand command)
    {
        var mudancas = new List<string>();

        if (produto.Nome != command.Nome)
        {
            mudancas.Add($"Nome: {produto.Nome} → {command.Nome}");
        }

        if (produto.Tipo != command.Tipo)
        {
            mudancas.Add($"Tipo: {produto.Tipo} → {command.Tipo}");
        }

        if (produto.PrecoVenda != command.PrecoVenda)
        {
            mudancas.Add($"Preço de venda: {produto.PrecoVenda.ToString("C")} → {command.PrecoVenda.ToString("C")}");
        }

        if (produto.PrecoCusto != command.PrecoCusto)
        {
            mudancas.Add($"Preço de custo: {produto.PrecoCusto.ToString("C")} → {command.PrecoCusto.ToString("C")}");
        }

        if (produto.PrecoGasDoPovo != command.PrecoGasDoPovo)
        {
            mudancas.Add($"Preço Gás do Povo: {produto.PrecoGasDoPovo.ToString("C")} → {command.PrecoGasDoPovo.ToString("C")}");
        }

        if (produto.EstoqueMinimo != command.EstoqueMinimo)
        {
            mudancas.Add($"Estoque mínimo: {produto.EstoqueMinimo} → {command.EstoqueMinimo}");
        }

        if (produto.Ativo != command.Ativo)
        {
            var de = produto.Ativo ? "Ativo" : "Inativo";
            var para = command.Ativo ? "Ativo" : "Inativo";
            mudancas.Add($"Status: {de} → {para}");
        }

        return mudancas;
    }
}