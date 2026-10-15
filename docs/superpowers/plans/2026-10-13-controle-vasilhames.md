# Controle de Vasilhames — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar controle de vasilhames vazios (disponíveis e emprestados) a produtos, vendas e clientes da revendedora.

**Architecture:** Vertical Slice. `Produto` ganha flag + contadores; `ItemVenda` ganha `EhEmprestimo`; nova entidade `EmprestimoVasilhame` como fonte da verdade de "quem/quanto". Handlers ajustam a venda (troca vs empréstimo), movimentação manual (prompt de vazios), empréstimo/devolução manuais (tela do cliente e estoque) e preservam a `DataHora` ao editar venda.

**Tech Stack:** .NET 10, EF Core + Npgsql, Mediator (martinothamar), FluentValidation, MudBlazor, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-13-controle-vasilhames-design.md`

## Global Constraints

- Mediator (pacote `Mediator` martinothamar), **sem** MediatR 13+.
- `Directory.Packages.props` centraliza versões NuGet; nenhum `.csproj` declara `Version`.
- Migrations EF geradas de `AndreGas.Infrastructure` (`dotnet-ef migrations add Nome --project src/AndreGas.Infrastructure --startup-project src/AndreGas.Infrastructure -o Migrations`).
- Priorizar `InMemoryDbContextFactory` em unit tests; `DatabaseFixture` só em integração.
- Nomes de domínio/telas/mensagens em **pt-BR**; tipos/namespaces C# em PascalCase.
- Cada slice tem testes **antes** da implementação (TDD red-green-refactor).
- Handlers de mutação de estoque/usuário reforçam com `RequerAdminAsync`.
- Regra de integridade: `Vasilhames ≥ 0` sempre; lançar `InvalidOperationException` quando a troca superar os disponíveis.

## Review Focus

1. **Venda em empréstimo excede estoque cheio disponível** — esperado: venda persiste e `Emprestados` do cliente sobe; `Estoque` cheio pode ficar abaixo (coberto por `Emprestados` do próprio cliente/estorno). → Handler valida `Estoque + EmprestadosDoCliente ≥ Qtd`.
2. **Estoque cheio vira negativo em troca** — esperado: `InvalidOperationException` com mensagem clara quando `Vasilhames disponíveis < Qtd` para troca. → Teste no handler.
3. **Edição de venda altera a data** — esperado: mantém a `DataHora` original; a Nova Venda usa `UtcNow`. → Teste no handler de edição.
4. **Devolução maior que o emprestado em aberto** — esperado: `InvalidOperationException` "Quantidade devolvida não pode exceder os vasilhames emprestados". → Teste no handler de devolução.
5. **Sem controle de vasilhame no produto** — esperado: checkbox "É empréstimo" não aparece e o handler ignora contadores (troca normal). → Teste no handler de venda.

---

### Task 1: Entidade `Produto` — flag e contadores de vasilhame

**Files:**
- Modify: `src/AndreGas.Domain/Entities/Produto.cs`
- Test: `tests/AndreGas.UnitTests/Domain/ProdutoTests.cs`

**Interfaces:**
- Consumes: nada (entidade base existente).
- Produces: `Produto.ControlaVasilhame` (bool), `Produto.QuantidadeVasilhames` (int, default 0), `Produto.QuantidadeVasilhamesEmprestados` (int, default 0), `Produto.TotalVasilhames` (read-only, `Vasilhames + Emprestados`), e métodos:
  - `RegistrarEntradaVasilhame(int)`, `RegistrarSaidaVasilhame(int)` (lança `ArgumentOutOfRangeException` p/ quantidade ≤ 0 e para saída > disponíveis),
  - `RegistrarEmprestimoVasilhame(int)` (Emprestados += qtd),
  - `RegistrarDevolucaoVasilhame(int)` (Emprestados −= qtd, lança se > Emprestados).
  - `AtualizarDados` passa a receber `bool controlaVasilhame`.

- [ ] **Step 1: Write the failing test** — adicionar casos a `ProdutoTests` cobrindo os métodos novos: entrada/saída de vasilhame, saída acima do disponível lança, empréstimo/devolução e guarda de devolução excedente, `TotalVasilhames`, e `AtualizarDados` com flag.

```csharp
[Fact]
public void RegistrarEntradaVasilhame_deve_somar_aos_vazios_disponiveis()
{
    var p = new Produto("Botijão", TipoProduto.Gas, 120m, 80m, 100m);
    p.RegistrarEntradaVasilhame(3);
    Assert.Equal(3, p.QuantidadeVasilhames);
    Assert.Equal(3, p.TotalVasilhames);
}
```
(mais casos equivalentes para saída/empréstimo/devolução + guardas + `AtualizarDados`)

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~ProdutoTests" -v` → FAIL (métodos não existem).

- [ ] **Step 3: Implement campos e métodos no `Produto`** — seguindo o estilo existente (`AjustarEstoque`, guardas). `AtualizarDados(..., bool controlaVasilhame)`.

- [ ] **Step 4: Run to verify it passes** — mesmo comando → PASS.

- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Domain/Entities/Produto.cs tests/AndreGas.UnitTests/Domain/ProdutoTests.cs
git commit -m "feat: produto ganha flag e contadores de vasilhame"
```

---

### Task 2: Entidade `ItemVenda.EhEmprestimo` + novos enums

**Files:**
- Modify: `src/AndreGas.Domain/Entities/ItemVenda.cs`
- Modify: `src/AndreGas.Domain/Enums/TipoHistoricoEstoque.cs`
- Modify: `src/AndreGas.Domain/Enums/TipoMovimentacaoEstoque.cs`
- Test: `tests/AndreGas.UnitTests/Domain/VendaTests.cs`, `tests/AndreGas.UnitTests/Domain/HistoricoEstoqueTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `ItemVenda.EhEmprestimo` (bool, public get, private set, default false). Novo ctor param opcional `bool ehEmprestimo = false`. Enums: `TipoHistoricoEstoque.EmprestimoVasilhame`, `DevolucaoVasilhame`, `Vasilhame`; `TipoMovimentacaoEstoque` já tem `Entrada/Saida/Ajuste` (não muda).

- [ ] **Step 1: Write the failing test** — em `VendaTests`, criar venda, `venda.AdicionarItem(..., ehEmprestimo: true)` e asserir `item.EhEmprestimo`; e por padrão `false`. Em `HistoricoEstoqueTests`, usar os novos tipos do enum.

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~VendaTests|FullyQualifiedName~HistoricoEstoqueTests"` → FAIL.

- [ ] **Step 3: Implement** — adicionar campo + param no ctor de `ItemVenda`; adicionar os 3 valores ao enum `TipoHistoricoEstoque`.

- [ ] **Step 4: Run to verify it passes** — mesmo comando → PASS.

- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Domain/Entities/ItemVenda.cs src/AndreGas.Domain/Enums/TipoHistoricoEstoque.cs tests/
git commit -m "feat: item de venda registra empréstimo; enums de histórico de vasilhame"
```

---

### Task 3: Entidade `EmprestimoVasilhame` + configuração EF + DbSet

**Files:**
- Create: `src/AndreGas.Domain/Entities/EmprestimoVasilhame.cs`
- Create: `src/AndreGas.Infrastructure/Configurations/EmprestimoVasilhameConfiguration.cs`
- Modify: `src/AndreGas.Infrastructure/AppDbContext.cs`
- Test: `tests/AndreGas.UnitTests/Domain/EmprestimoVasilhameTests.cs` (novo)

**Interfaces:**
- Consumes: `Produto`, `Cliente`.
- Produces: entidade `EmprestimoVasilhame` (Id, ClienteId, Cliente?, ProdutoId, Produto?, Quantidade, Data, Observacao?, DevolvidoEm?, QuantidadeDevolvida, propriedade `Saldo => Quantidade - QuantidadeDevolvida`), `AppDbContext.EmprestimosVasilhames` (DbSet). Ctor público com `(Guid clienteId, Guid produtoId, int quantidade, DateTime? data, string? observacao)`; método `RegistrarDevolucao(int quantidade)` que ajusta `QuantidadeDevolvida`/`DevolvidoEm`.

- [ ] **Step 1: Write the failing test** — novo `EmprestimoVasilhameTests`: ctor, `Saldo` inicial, `RegistrarDevolucao` parcial/até quitar (define `DevolvidoEm`), guarda de devolução > saldo.

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~EmprestimoVasilhameTests"` → FAIL.

- [ ] **Step 3: Implement** entidade + config (`ToTable("EmprestimosVasilhames")`, FKs Restrict, `DevolvidoEm`/`QuantidadeDevolvida`) + `DbSet` no DbContext + índices para (ClienteId, ProdutoId).

- [ ] **Step 4: Run to verify it passes**.

- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Domain/Entities/EmprestimoVasilhame.cs src/AndreGas.Infrastructure/Configurations/EmprestimoVasilhameConfiguration.cs src/AndreGas.Infrastructure/AppDbContext.cs tests/AndreGas.UnitTests/Domain/EmprestimoVasilhameTests.cs
git commit -m "feat: entidade EmprestimoVasilhame e DbSet"
```

---

### Task 4: Migração EF inicial + backfill

**Files:**
- Create: `src/AndreGas.Infrastructure/Migrations/<timestamp>_AdicionarControleVasilhames.cs` (+.Designer)
- Modify: `src/AndreGas.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: Tasks 1-3 (campos no modelo).
- Produces: schema migrado — colunas `ControlaVasilhame`/`QuantidadeVasilhames`/`QuantidadeVasilhamesEmprestados` na `Produtos`, `EhEmprestimo` na `ItensVenda`, tabela `EmprestimosVasilhames`; backfill `ControlaVasilhame=true` para tipos de gás/água (Botijão Gás 13kg, 45kg, Galão Água 20L).

- [ ] **Step 1: Generate migration**
```bash
dotnet-ef migrations add AdicionarControleVasilhames --project src/AndreGas.Infrastructure --startup-project src/AndreGas.Infrastructure -o Migrations
```
- [ ] **Step 2: Add backfill to `Up()`** — `UPDATE "Produtos" SET "ControlaVasilhame" = true WHERE "Nome" IN ('Botijão Gás 13kg','Botijão Gás 45kg','Galão Água 20L');`. Incluir a mesma regra no seed (Task 9).

- [ ] **Step 3: Verify build** — `dotnet build` (sem erros).

- [ ] **Step 4: Commit**
```bash
git add src/AndreGas.Infrastructure/Migrations/
git commit -m "feat: migração AdicionarControleVasilhames com backfill de gases/água"
```

---

### Task 5: `RegistrarVendaCommand` + Handler — troca vs empréstimo (core)

**Files:**
- Modify: `src/AndreGas.Web/Features/Vendas/NovaVenda/RegistrarVendaCommand.cs` (`ItemVendaInput` ganha `bool EhEmprestimo = false`)
- Modify: `src/AndreGas.Web/Features/Vendas/NovaVenda/RegistrarVendaCommandHandler.cs`
- Modify: `src/AndreGas.Web/Features/Vendas/NovaVenda/RegistrarVendaCommandValidator.cs`
- Test: `tests/AndreGas.UnitTests/Features/Vendas/RegistrarVendaCommandHandlerTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3. `RegistrarAsync(db, command, vendedorId, usuario, cancellationToken, DateTime? dataHora = null)` (dataHora opcional para a edição).
- Produces: venda com itens `EhEmprestimo`; ao item **empréstimo** cria `EmprestimoVasilhame` com `Data = dataHora ?? agora`; handler aplica: troca → `Estoque−, Vasilhames+`; empréstimo → `Estoque−, Emprestados+`; cria `HistoricoEstoque` tipo `Vasilhame`/`EmprestimoVasilhame`.

- [ ] **Step 1: Write the failing tests** — casos:
  - troca: `RegistrarAsync` com `ehEmprestimo:false` → produto cheio `Estoque−`, `Vasilhames+`.
  - empréstimo: `ehEmprestimo:true` → `Estoque−`, `Emprestados+`, e um `EmprestimoVasilhame` aberto registrado ao cliente com `Data = agora`.
  - **respeito a estoque cheio**: empréstimo X onde `Estoque < X` mas `Emprestados do cliente ≥ X−Estoque` → **passa**.
  - troca com `Vasilhames < Qtd` → `InvalidOperationException`.
- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~RegistrarVendaCommandHandlerTests"` → FAIL (campos novos não existem).
- [ ] **Step 3: Implement** — novo comportamento no loop de itens de `RegistrarAsync` (mover lógica de estoque p/ branching por `EhEmprestimo`); registrar `EmprestimoVasilhame` p/ itens de empréstimo; `ItemVenda.AdicionarItem(..., ehEmprestimo)`.
- [ ] **Step 4: Run to verify it passes**.
- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Web/Features/Vendas/NovaVenda/ tests/AndreGas.UnitTests/Features/Vendas/RegistrarVendaCommandHandlerTests.cs
git commit -m "feat: venda com troca vs empréstimo de vasilhame"
```

---

### Task 6: `RegistrarMovimentacaoEstoqueCommand` + Handler — prompt de vasilhames

**Files:**
- Modify: `src/AndreGas.Web/Features/Estoque/Movimentar/RegistrarMovimentacaoEstoqueCommand.cs`
- Modify: `src/AndreGas.Web/Features/Estoque/Movimentar/RegistrarMovimentacaoEstoqueCommandHandler.cs`
- Test: `tests/AndreGas.UnitTests/Features/Estoque/RegistrarMovimentacaoEstoqueCommandHandlerTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces: comando com `bool AdicionarVasilhames = false`, `int QuantidadeVasilhamesAdicionar = 0`. Handler:
  - Entrada: `Estoque += N`, `Vasilhames += N`; se `AdicionarVasilhames` → `Vasilhames += QuantidadeVasilhamesAdicionar`; senão cria `HistoricoEstoque` tipo `Vasilhame` (entrada equivalente).
  - Saída: `Estoque − N`, `Vasilhames += N`.
  - Ajuste: só `Estoque = N` (sem tocar vazios).
- Testes novos (unit): entrada com flag, sem flag, saída volta vazio, ajuste não toca vazios, histórico correto.

- [ ] **Step 1: Write the failing tests** — casos conforme Produces.
- [ ] **Step 2: Run to verify it fails**.
- [ ] **Step 3: Implement** — branch nos switches por tipo e por flag; ajustar `CapturarMudancas`/descrições de histórico.
- [ ] **Step 4: Run to verify it passes**.
- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Web/Features/Estoque/Movimentar/ tests/AndreGas.UnitTests/Features/Estoque/RegistrarMovimentacaoEstoqueCommandHandlerTests.cs
git commit -m "feat: movimentação de estoque com prompt de vasilhames na entrada"
```

---

### Task 7: `MovimentarEstoqueDialog` — prompt "adicionar vasilhames automaticamente?"

**Files:**
- Modify: `src/AndreGas.Web/Features/Estoque/Listar/MovimentarEstoqueDialog.razor`
- Consumes: Task 6 (campos novos do comando) + `ListarProdutosQuery` (para saber `QuantidadeVasilhames` do produto).
- Test: — (UI; cobertura indireta via handler da Task 6).

- [ ] **Step 1: Implement** — no `ConfirmarAsync`, para `_tipo == Entrada`, carregar o produto (via `ObterProdutoQuery`) e se `_quantidade > produto.QuantidadeVasilhames`, abrir `MudDialog` de confirmação "Deseja adicionar vasilhames automaticamente?" com `MudNumericField` entre `0` e `(_quantidade − produto.QuantidadeVasilhames)`. Sim → `AdicionarVasilhames=true`, campo = diferença; Não → `false`. Enviar o comando.
- [ ] **Step 2: Build** — `dotnet build` (sem erros).
- [ ] **Step 3: Commit**
```bash
git add src/AndreGas.Web/Features/Estoque/Listar/MovimentarEstoqueDialog.razor
git commit -m "feat: prompt de vasilhames automáticos na entrada de estoque"
```

---

### Task 8: Handlers de empréstimo/devolução manuais

**Files:**
- Create: `src/AndreGas.Web/Features/Clientes/Emprestimos/RegistrarEmprestimoCommand.cs` (+Handler, +Validator)
- Create: `src/AndreGas.Web/Features/Clientes/Emprestimos/RegistrarDevolucaoVasilhameCommand.cs` (+Handler, +Validator)
- Create: `src/AndreGas.Web/Features/Clientes/Emprestimos/ObterEmprestimosClienteQuery.cs` (+Handler)
- Test: `tests/AndreGas.UnitTests/Features/Clientes/Emprestimos/RegistrarEmprestimoCommandHandlerTests.cs`, `.../RegistrarDevolucaoVasilhameCommandHandlerTests.cs`, `.../ObterEmprestimosClienteQueryHandlerTests.cs` (novos)

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces:
  - `RegistrarEmprestimoCommand(ClienteId, ProdutoId, Quantidade, Observacao?) : ICommand<Guid>` — cria `EmprestimoVasilhame` aberto; `Produto.RegistrarEmprestimoVasilhame(Quantidade)`; histórico `EmprestimoVasilhame`. Valida `Quantidade > 0` e produto controla vasilhame.
  - `RegistrarDevolucaoVasilhameCommand(ClienteId, ProdutoId, Quantidade, Observacao?) : ICommand<bool>` — cascata de 3 regras (quita do Emprestados; se há estoque de cheio disponível re-abastece; senão mantém). Lança se devolução > em aberto.
  - `ObterEmprestimosClienteQuery(ClienteId) : IQuery<IReadOnlyList<EmprestimoClienteItem>>` — empréstimos abertos do cliente (Produto, Quantidade, Data).
- Ambos restritos a Admin (injetar `IUsuarioAutenticado`).

- [ ] **Step 1: Write the failing tests** — casos de Produces (incluindo devolução > saldo lança; devolução parcial; sem estoque disponível mantém Estoque; com estoque disponível re-abastece).
- [ ] **Step 2: Run to verify it fails**.
- [ ] **Step 3: Implement** os 3 commands/handlers + validators.
- [ ] **Step 4: Run to verify it passes**.
- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Web/Features/Clientes/Emprestimos/ tests/AndreGas.UnitTests/Features/Clientes/Emprestimos/
git commit -m "feat: registrar empréstimo e devolução manual de vasilhames"
```

---

### Task 9: Zero queries — `ListarProdutos`, `ObterProduto`, `ListarClientes`, `ObterClienteDetalhe`

**Files:**
- Modify: `src/AndreGas.Web/Features/Estoque/Listar/ListarProdutosQuery.cs` + Handler (acrescentar `ControlaVasilhame`, `QuantidadeVasilhames`, `QuantidadeVasilhamesEmprestados`, `TotalVasilhames`)
- Modify: `src/AndreGas.Web/Features/Estoque/Editar/ObterProdutoQuery.cs` + Handler (idem result)
- Modify: `src/AndreGas.Web/Features/Clientes/Listar/ListarClientesQuery.cs` + Handler (acrescentar `QuantidadeVasilhamesEmprestados` no item — coluna)
- Modify: `src/AndreGas.Web/Features/Clientes/Detalhe/ObterClienteDetalheQuery.cs` + Handler (result ganha `Emprestimos` — lista de `EmprestimoClienteItem`)
- Test: correspondentes em `tests/AndreGas.UnitTests/Features/{Estoque,Clientes}/...`

**Interfaces:**
- Consumes: Tasks 1-3 (campos novos) e Task 8 (`EmprestimoClienteItem`).
- Produces: novos campos nas queries de listagem/detalhe, alimentando as telas.

- [ ] **Step 1: Write the failing tests** — asserir novos campos preenchidos a partir de dados com vasilhames.
- [ ] **Step 2: Run to verify it fails**.
- [ ] **Step 3: Implement** — ajustar records e projeções LINQ.
- [ ] **Step 4: Run to verify it passes**.
- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Web/Features/Estoque/ src/AndreGas.Web/Features/Clientes/ tests/
git commit -m "feat: queries expõem contadores de vasilhame"
```

---

### Task 10: Reversão de venda (excluir + editar) e preservação da DataHora

**Files:**
- Modify: `src/AndreGas.Web/Features/Admin/Vendas/ReversaoVendaHelper.cs`
- Modify: `src/AndreGas.Web/Features/Admin/Vendas/EditarVendaCommandHandler.cs`
- Modify: `src/AndreGas.Web/Features/Admin/Vendas/EditarVendaCommandValidator.cs` (validar itens de empréstimo com `Estoque + EmprestadosDoCliente`)
- Test: `tests/AndreGas.UnitTests/Features/Admin/Vendas/EditarVendaCommandHandlerTests.cs`, `tests/AndreGas.UnitTests/Features/Admin/Vendas/ExcluirVendaCommandHandlerTests.cs`

**Interfaces:**
- Consumes: Tasks 3, 5 (`RegistrarAsync` com `dataHora`), Task 8.
- Produces: `ReverterAsync` extremo reversão de empréstimo: quando `item.EhEmprestimo` → `Produto.RegistrarDevolucaoVasilhame(qtd)` + `Emprestados do cliente −qtd` + `Estoque + qtd`; senão troca → `Vasilhames − qtd`, `Estoque + qtd`. `EditarVendaCommandHandler` passa `venda.DataHora` para `RegistrarAsync` (nova venda preserva data).

- [ ] **Step 1: Write the failing tests** — editar venda de empréstimo reversa `Emprestados`; editar venda de troca reversa `Vasilhames`; **data original preservada** após editar (`venda.DataHora` == original). Também excluir venda de empréstimo.
- [ ] **Step 2: Run to verify it fails**.
- [ ] **Step 3: Implement** — atualizar `ReversaoVendaHelper.ReverterAsync`; `EditarVendaCommandHandler` passa `dataHora`; validator ajustado.
- [ ] **Step 4: Run to verify it passes**.
- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Web/Features/Admin/Vendas/ tests/AndreGas.UnitTests/Features/Admin/Vendas/
git commit -m "feat: edição/exclusão de venda reverte vasilhames e preserva data"
```

---

### Task 11: UI — `NovaVendaDialog` e `EditarVendaDialog` com checkbox "É empréstimo" por item

**Files:**
- Modify: `src/AndreGas.Web/Features/Vendas/NovaVenda/NovaVendaDialog.razor`
- Modify: `src/AndreGas.Web/Features/Admin/Vendas/EditarVendaDialog.razor`
- Modify: `src/AndreGas.Web/Features/Admin/Vendas/ObterVendaParaEdicaoQuery` (item `VendaItemEdicao` ganha `EhEmprestimo`; result ganha `DataHora`)
- Test: — (UI; cobertura via handlers Tasks 5/10).

**Interfaces:**
- Consumes: Tasks 5, 10 (campos `EhEmprestimo`); `ProdutoListItem.ControlaVasilhame` da Task 9.
- Produces: checkbox "É empréstimo" (default desmarcado) ao lado da **quantidade**, **apenas para itens cujo produto `ControlaVasilhame`**. No `EditarVendaDialog`, `DataHora` da venda mostrada de forma **somente leitura** (não editável).

- [ ] **Step 1: Implement** `NovaVendaDialog` — `ItemVendaEdit` ganha `EhEmprestimo`; renderiza `MudSwitch`/`MudCheckBox` condicional; envia `EhEmprestimo` no `ItemVendaInput`.
- [ ] **Step 2: Implement** `EditarVendaDialog` + query — adicionar `EhEmprestimo` em `VendaItemEdicao` e `DataHora` no result; checkbox por item; DataHora read-only.
- [ ] **Step 3: Build** — `dotnet build`.
- [ ] **Step 4: Commit**
```bash
git add src/AndreGas.Web/Features/Vendas/NovaVenda/NovaVendaDialog.razor src/AndreGas.Web/Features/Admin/Vendas/
git commit -m "feat: checkbox é-emprestimo por item nas vendas e edição"
```

---

### Task 12: UI — tela de estoque "Controle de Vasilhames" + modal "para quem"

**Files:**
- Create: `src/AndreGas.Web/Features/Estoque/Vasilhames/ObterControleVasilhamesQuery.cs` (+Handler)
- Modify: `src/AndreGas.Web/Features/Estoque/Listar/EstoqueLista.razor`
- Create: `src/AndreGas.Web/Features/Estoque/Vasilhames/VasilhamesEmprestadosDialog.razor` (novo)
- Test: `tests/AndreGas.UnitTests/Features/Estoque/Vasilhames/ObterControleVasilhamesQueryHandlerTests.cs`

**Interfaces:**
- Consumes: Tasks 8 (`RegistrarEmprestimoCommand`, `RegistrarDevolucaoVasilhameCommand`), Task 9 (`ProdutoListItem.ControlaVasilhame`).
- Produces: query `ObterControleVasilhamesQuery : IQuery<IReadOnlyList<ControleVasilhameItem>>` com (ProdutoId, Nome, Disponivel, Emprestados, Total, IReadOnlyList<EmprestimoClienteItem>). Tela lista apenas produtos `ControlaVasilhame`; linha abre modal mostrando "para quem" (clientes com `Emprestados > 0`). Botões registrar empréstimo/devolução na tela de estoque.

- [ ] **Step 1: Write the failing tests** — query retorna agregado correto (disponíveis/emprestados/total) e lista de clientes.
- [ ] **Step 2: Run to verify it fails**.
- [ ] **Step 3: Implement** query + handler.
- [ ] **Step 4: Implement** `EstoqueLista.razor` — seção "Controle de Vasilhames" (só admin) + modal + botões.
- [ ] **Step 5: Build + test** — `dotnet build` + `dotnet test tests/AndreGas.UnitTests`.
- [ ] **Step 6: Commit**
```bash
git add src/AndreGas.Web/Features/Estoque/Vasilhames/ src/AndreGas.Web/Features/Estoque/Listar/EstoqueLista.razor tests/
git commit -m "feat: tela de estoque mostra controle de vasilhames e para quem"
```

---

### Task 13: UI — tela do cliente (saldo + registrar empréstimo/devolução)

**Files:**
- Modify: `src/AndreGas.Web/Features/Clientes/Detalhe/ClienteDetalhe.razor`
- Consumes: Tasks 8, 9 (`ObterEmprestimosClienteQuery`, `ObterClienteDetalheQuery` resultado com `Emprestimos`).
- Produces: seção "Vasilhames emprestados" listando produto/qtd; botões **Registrar empréstimo** e **Registrar devolução** (chamam handlers da Task 8); coluna prévia em `ClientesLista.razor` (opcional).

- [ ] **Step 1: Implement** — seção no `ClienteDetalhe.razor` com lista de saldo + botões + pequenos diálogos para quantidade/observação.
- [ ] **Step 2: Build** — `dotnet build`.
- [ ] **Step 3: Implement** coluna em `ClientesLista.razor` (opcional, se desejado).
- [ ] **Step 4: Commit**
```bash
git add src/AndreGas.Web/Features/Clientes/Detalhe/ClienteDetalhe.razor src/AndreGas.Web/Features/Clientes/Listar/ClientesLista.razor
git commit -m "feat: tela do cliente mostra e registra empréstimos de vasilhames"
```

---

### Task 14: UI — cadastro/edição de produto (checkbox + total)

**Files:**
- Modify: `src/AndreGas.Web/Features/Estoque/Cadastrar/CadastrarProdutoCommand.cs` (novo `bool ControlaVasilhame = false`)
- Modify: `src/AndreGas.Web/Features/Estoque/Cadastrar/ProdutoCadastro.razor`
- Modify: `src/AndreGas.Web/Features/Estoque/Editar/EditarProdutoDialog.razor`
- Modify: `src/AndreGas.Web/Features/Estoque/Cadastrar/CadastrarProdutoCommandHandler.cs`
- Modify: `src/AndreGas.Web/Features/Estoque/Editar/AtualizarProdutoCommand.cs` + `Handler` (passar `ControlaVasilhame`)
- Test: `tests/AndreGas.UnitTests/Features/Estoque/CadastrarProdutoCommandHandlerTests.cs`, `.../AtualizarProdutoCommandHandlerTests.cs`

**Interfaces:**
- Consumes: Tasks 1 (métodos/flag), 9 (`ObterProdutoQuery` com `QuantidadeVasilhames` etc).
- Produces: cadastro/edição aceitam/persistem `ControlaVasilhame`; edição exibe **Total de vasilhames** (read-only).

- [ ] **Step 1: Write the failing tests** — handler persiste flag no cadastro e na edição; validators aceitam o campo.
- [ ] **Step 2: Run to verify it fails**.
- [ ] **Step 3: Implement** — command/handlers + DOM (checkbox + total read-only).
- [ ] **Step 4: Run to verify it passes**.
- [ ] **Step 5: Commit**
```bash
git add src/AndreGas.Web/Features/Estoque/Cadastrar/ src/AndreGas.Web/Features/Estoque/Editar/ tests/
git commit -m "feat: cadastro e edição de produto com controle de vasilhame"
```

---

### Task 15: Seed + ajustes de integração

**Files:**
- Modify: `src/AndreGas.Web/Features/Seed/SeedDemoData.cs` (produtos de gás/água com `ControlaVasilhame=true`; registrar empréstimos/devoluções de exemplo)
- Create: `tests/AndreGas.IntegrationTests/VasilhamesFeatureTests.cs` (novo)
- Consumes: tudo acima.

- [ ] **Step 1: Update seed** — `ControlaVasilhame=true` para gás/água; criar alguns `EmprestimoVasilhame`/devoluções nos clientes demo.
- [ ] **Step 2: Write integration tests** — fluxo real via `DatabaseFixture`: venda empréstimo → `Emprestados` sobe; devolução → cai/re-abastece; estorno de venda de empréstimo; "para quem".
- [ ] **Step 3: Run integration tests** — `dotnet test tests/AndreGas.IntegrationTests --filter "FullyQualifiedName~VasilhamesFeatureTests"`.
- [ ] **Step 4: Commit**
```bash
git add src/AndreGas.Web/Features/Seed/SeedDemoData.cs tests/AndreGas.IntegrationTests/VasilhamesFeatureTests.cs
git commit -m "feat: seed e testes de integração de vasilhames"
```

---

### Task 16: Revisão final de build/tests + `dotnet format`

**Files:**
- Nenhum código novo.

- [ ] **Step 1: Full build** — `dotnet build`.
- [ ] **Step 2: Full test** — `dotnet test` (unit + integração).
- [ ] **Step 3: Format** — `dotnet format` e revisar o diff de formatação (commit separado se alterou).
- [ ] **Step 4: Commit** (se formatação gerou diferenças)
```bash
git add -u
git commit -m "chore: formatação pós-implementação de vasilhames"
```

---

## Self-Review

**Spec coverage:**
- Produto flag/contadores → Task 1 ✅
- ItemVenda.EhEmprestimo → Task 2 ✅
- EmprestimoVasilhame fonte da verdade → Task 3 ✅
- Migração + backfill → Task 4 ✅
- Nova Venda troca vs empréstimo + integridade + validator → Task 5 ✅
- Movimentação manual + prompt → Tasks 6-7 ✅
- Empréstimos manuais + devolução cascata → Task 8 ✅
- Queries (estoque, detalhe, lista) → Task 9 ✅
- Edição/exclusão reversão + data preservada → Task 10 ✅
- Checkbox na venda/edição → Task 11 ✅
- Tela estoque "Controle de Vasilhames" + modal → Task 12 ✅
- Tela cliente saldo + registrar empréstimo/devolução → Task 13 ✅
- Cadastro/edição produto checkbox + total → Task 14 ✅
- Seed + integração → Task 15 ✅

**Step scan:** cada passo é uma ação testável única (test step → fail → implement → pass → commit). Nenhuma linha decide nada sozinha.

**Type consistency:**
- `Produto.QuantidadeVasilhames`, `.QuantidadeVasilhamesEmprestados`, `TotalVasilhames`, `ControlaVasilhame` consistentes nas Tasks 1, 5, 6, 9, 14.
- `ItemVenda.EhEmprestimo`, ctor param `ehEmprestimo` consistentes nas Tasks 2, 5, 10, 11.
- `EmprestimoVasilhame.RegistrarDevolucao`, `Saldo` consistentes nas Tasks 3, 8, 15.
- `RegistrarAsync(..., dataHora = null)` consistente nas Tasks 5, 10.
- `EmprestimoClienteItem` (Produto, Quantidade, Data) consistente nas Tasks 8, 9, 12.

**Review Focus:** os 5 itens da seção "Review Focus" têm testes nas Tasks 5, 10, 8 (empréstimo sobre estoque, troca insuficiente, data preservada, devolução > saldo, sem controle).

**Proportion:** plano ~2× a spec, proporcional (14 tasks de código + 2 de revisão) — sem transcrição de corpos. ✅