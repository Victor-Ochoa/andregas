# Aviso de Recompra na Home — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Home mostra quais clientes provavelmente precisam recomprar (cliente×produto com `AvisarRecompra`), com intervalo aprendido das vendas reais, e ação "Registrar venda" que abre a Nova Venda pré-selecionada.

**Architecture:** Resumo incremental (sem worker). `RecorrenciaCompra` por Cliente×Produto é recalculada das **vendas vigentes** sempre que o conjunto de vendas de um par muda (nova venda, edição, exclusão). A Home lê a tabela com `DataPrevista = DataUltimaCompra + IntervaloMedioDias <= hoje` dentro de uma janela e limite 10.

**Tech Stack:** .NET 10, EF Core + Npgsql, Mediator (martinothamar), FluentValidation, MudBlazor, xUnit + InMemory (unit) / Postgres via Aspire (integração).

**Spec:** `docs/superpowers/specs/2026-10-22-recompra-avisos-design.md`

## Global Constraints

- **Não usar MediatR ≥ 13** — usar `Mediator` (martinothamar), registrado `ServiceLifetime.Scoped`.
- **VSA**: cada slice em `AndreGas.Web/Features/<Area>/<CasoDeUso>/` com Command/Query, Handler, Validator e componentes `.razor` juntos. Sem camadas horizontais (`Services/`, `Repositories/`).
- **TDD obrigatório**: escrever o teste primeiro (ver falhar), implementar o mínimo, refatorar.
- **MudBlazor** para todos os componentes de UI; tema azul marinho+branco já definido.
- **Central Package Management**: versões só em `Directory.Packages.props`; `.csproj` sem `Version`.
- **Validação automática**: `ValidationBehavior` roda qualquer `IValidator<TMessage>` antes do handler.
- **`TimeProvider`** injetado nos handlers de leitura (padrão `ObterDashboardQueryHandler`).
- **Fuso**: datetimes gravados em UTC; exibição em Brasília via `BrasilTimeZone` (`ParaBrasilia`/`ParaUtc`).
- **Nomes/telas/mensagens em pt-BR**; tipos C# em PascalCase.
- **Não commitar na main** — usar branch `feat/...` + merge request.
- **Conventional Commits** — `feat:`, `test:`, `chore:`, `docs:`.
- **Fiado/Gás do Povo**: forma de pagamento **não** afeta aviso (toda venda vigente conta).

## Review Focus

Inputs que a spec implica, mas nenhum teste de tarefa exercita sozinho — comportamentos que um usuário razoável espera:

1. **Produto com `AvisarRecompra = true` mas `DiasPadraoRecompra = 0` e só 1 compra** → nunca aparece (intervalo 0). Deve **não quebrar** a query de dashboard e **não** aparecer como pendente. → testado em Task 5.
2. **Recompra de produto inativo** → deve aparecer na Home (ligar, não vender) e o botão "Registrar venda" deve funcionar (abre Nova Venda com cliente, mesmo que produto inativo não seja vendável — o operador liga para oferecer outro produto). → testado em Task 7.
3. **Cliente excluído/soft e produto excluído** → como `Cliente` não tem soft delete e `Produto` não é removido (só `Ativo=false`), a query de recompra deve **ignorar** qualquer par cujo produto foi removido do banco (RR de Restrict impede remoção com vendas, mas o seed/purge poderia). Proteção: `Include(Produto)` e pular `Produto == null`. → testado em Task 5.
4. **Gap de 0 dias entre duas vendas do mesmo produto no mesmo dia** → ignorado (gap < 7d); se as duas compras forem no mesmo dia, `IntervaloMedioDias` cai para o **default do produto** (não 0). → testado em Task 2.
5. **Venda fiado com `Status = FiadoAberto`** → conta para a recorrência normalmente (não é o lucro que importa aqui, é a compra). → coberto em Task 2/5 e no próprio handler de venda (não precisa de teste dedicado, mas o comportamento não deve regredir).

---

### Task 1: Domain — `Produto` com `AvisarRecompra`/`DiasPadraoRecompra` + nova entidade `RecorrenciaCompra`

**Files:**
- Modify: `src/AndreGas.Domain/Entities/Produto.cs` (ctor + `AtualizarDados` + 2 props)
- Create: `src/AndreGas.Domain/Entities/RecorrenciaCompra.cs`
- Test: `tests/AndreGas.UnitTests/Domain/ProdutoTests.cs`, `tests/AndreGas.UnitTests/Domain/RecorrenciaCompraTests.cs`

**Interfaces:**
- Produces:
  - `Produto` ctor: `Produto(string nome, TipoProduto tipo, decimal precoVenda, decimal precoCusto, decimal precoGasDoPovo, int estoqueMinimo = 0, bool controlaVasilhame = false, bool avisarRecompra = false, int diasPadraoRecompra = 0)`
  - `Produto.AtualizarDados(string nome, TipoProduto tipo, decimal precoVenda, decimal precoCusto, decimal precoGasDoPovo, int estoqueMinimo, bool controlaVasilhame = false, bool avisarRecompra = false, int diasPadraoRecompra = 0)`
  - `Produto.AvisarRecompra` (bool), `Produto.DiasPadraoRecompra` (int) — getters sem setter.
  - `RecorrenciaCompra.Guid Id`, `Guid ClienteId`, `Guid ProdutoId`, `Cliente? Cliente`, `Produto? Produto`, `DateTime DataUltimaCompra`, `decimal IntervaloMedioDias`, `int QuantidadeCompras`. Ctor `(Guid clienteId, Guid produtoId, DateTime dataUltimaCompra, decimal intervaloMedioDias, int quantidadeCompras)` + `Atualizar(...)`.
- Consumes: nada de tasks anteriores.

- [ ] **Step 1: Write failing test — `Produto` guarda e valida flags**

```csharp
[Fact]
public void Construtor_ComAvisarRecompra_DevePersistirFlags()
{
    var p = new Produto("Botijão", TipoProduto.Gas, 100m, 60m, 80m,
        estoqueMinimo: 5, avisarRecompra: true, diasPadraoRecompra: 45);
    Assert.True(p.AvisarRecompra);
    Assert.Equal(45, p.DiasPadraoRecompra);
}

[Fact]
public void AtualizarDados_ComDiasPadraoRecompraNegativo_DeveLancar()
{
    var p = new Produto("Botijão", TipoProduto.Gas, 100m, 60m, 80m);
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        p.AtualizarDados("Botijão", TipoProduto.Gas, 100m, 60m, 80m, 5, avisarRecompra: true, diasPadraoRecompra: -1));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~ProdutoTests" -v`
Expected: FAIL — construtor não aceita os novos parâmetros.

- [ ] **Step 3: Implement `Produto` changes**

Adicionar props + parâmetros de ctor/`AtualizarDados` com guarda `diasPadraoRecompra >= 0` (lança `ArgumentOutOfRangeException`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~ProdutoTests" -v`
Expected: PASS.

- [ ] **Step 5: Write failing test — `RecorrenciaCompra`**

```csharp
[Fact]
public void Ctor_DeveGuardarEstado()
{
    var r = new RecorrenciaCompra(Guid.NewGuid(), Guid.NewGuid(),
        new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 45m, 2);
    Assert.Equal(45m, r.IntervaloMedioDias);
    Assert.Equal(2, r.QuantidadeCompras);
}

[Fact]
public void Atualizar_DeveSubstituirValores()
{
    var r = new RecorrenciaCompra(Guid.NewGuid(), Guid.NewGuid(),
        new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 45m, 2);
    var novaData = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    r.Atualizar(novaData, 37.5m, 3);
    Assert.Equal(novaData, r.DataUltimaCompra);
    Assert.Equal(37.5m, r.IntervaloMedioDias);
    Assert.Equal(3, r.QuantidadeCompras);
}
```

- [ ] **Step 6: Run test to verify it fails**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~RecorrenciaCompraTests" -v`
Expected: FAIL — tipo inexistente.

- [ ] **Step 7: Implement `RecorrenciaCompra`**

```csharp
public class RecorrenciaCompra
{
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }
    public DateTime DataUltimaCompra { get; private set; }
    public decimal IntervaloMedioDias { get; private set; }
    public int QuantidadeCompras { get; private set; }

    private RecorrenciaCompra() { }

    public RecorrenciaCompra(Guid clienteId, Guid produtoId, DateTime dataUltimaCompra,
        decimal intervaloMedioDias, int quantidadeCompras)
    {
        Id = Guid.NewGuid();
        ClienteId = clienteId;
        ProdutoId = produtoId;
        DataUltimaCompra = dataUltimaCompra;
        IntervaloMedioDias = intervaloMedioDias;
        QuantidadeCompras = quantidadeCompras;
    }

    public void Atualizar(DateTime dataUltimaCompra, decimal intervaloMedioDias, int quantidadeCompras)
    {
        DataUltimaCompra = dataUltimaCompra;
        IntervaloMedioDias = intervaloMedioDias;
        QuantidadeCompras = quantidadeCompras;
    }
}
```

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~RecorrenciaCompraTests" -v`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/AndreGas.Domain/Entities/Produto.cs src/AndreGas.Domain/Entities/RecorrenciaCompra.cs tests/AndreGas.UnitTests/Domain/ProdutoTests.cs tests/AndreGas.UnitTests/Domain/RecorrenciaCompraTests.cs
git commit -m "feat: domínio de recorrência de compra (Produto.AvisarRecompra + RecorrenciaCompra)"
```

---

### Task 2: Infra + Aprendizado — `RecorrenciaCompraAprendizado` (motor) + EF config + migração

**Files:**
- Modify: `src/AndreGas.Infrastructure/AppDbContext.cs` (DbSet + config)
- Create: `src/AndreGas.Infrastructure/Configurations/RecorrenciaCompraConfiguration.cs`
- Create: `src/AndreGas.Web/Common/RecorrenciaCompraAprendizado.cs` (motor estático)
- Test: `tests/AndreGas.UnitTests/Common/RecorrenciaCompraAprendizadoTests.cs`

**Interfaces:**
- Consumes: Task 1 — `RecorrenciaCompra` + `Produto.AvisarRecompra`/`DiasPadraoRecompra`.
- Produces:
  - `RecorrenciaCompraAprendizado.RecalcularAsync(AppDbContext db, Guid clienteId, Guid produtoId, CancellationToken ct)` — recalcula do zero a partir das vendas vigentes do par.
  - `AppDbContext.RecorrenciasCompra` (`DbSet<RecorrenciaCompra>`).
  - Migration `AdicionarRecorrenciaCompra`.

- [ ] **Step 1: Write failing test — aprendizado**

```csharp
using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Common;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Common;

public class RecorrenciaCompraAprendizadoTests
{
    private static async Task<(AppDbContext db, Guid clienteId, Guid produtoId)> CriarBaseAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
        // AvisarRecompra: true, DiasPadraoRecompra: 45
        var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
            avisarRecompra: true, diasPadraoRecompra: 45);
        db.Clientes.Add(cliente);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return (db, cliente.Id, produto.Id);
    }

    private static async Task<Venda> CriarVendaAsync(AppDbContext db, Guid clienteId, Guid produtoId, DateTime data, int quantidade = 1)
    {
        var venda = new Venda(clienteId, FormaPagamento.Dinheiro, data);
        venda.AdicionarItem(produtoId, quantidade, 100m, 60m);
        db.Vendas.Add(venda);
        return venda;
    }

    [Fact]
    public async Task Recalcular_UmaVenda_UsaDiasPadraoRecompra()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), rec.DataUltimaCompra);
        Assert.Equal(45m, rec.IntervaloMedioDias);
        Assert.Equal(1, rec.QuantidadeCompras);
    }

    [Fact]
    public async Task Recalcular_DuasVendas_ComGap45_CalculaMedia()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)); // gap 45
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(45m, rec.IntervaloMedioDias);
        Assert.Equal(2, rec.QuantidadeCompras);
        Assert.Equal(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc), rec.DataUltimaCompra);
    }

    [Fact]
    public async Task Recalcular_TresVendas_Gaps45E30_CalculaMedia37_5()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)); // gap 45
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc)); // gap 30
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(37.5m, rec.IntervaloMedioDias);
        Assert.Equal(3, rec.QuantidadeCompras);
    }

    [Fact]
    public async Task Recalcular_GapMenorQue7Dias_Ignorado_UsaDefaultDoProduto()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)); // gap 1 < 7
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(45m, rec.IntervaloMedioDias); // default, não gap de 1 dia
        Assert.Equal(2, rec.QuantidadeCompras);
    }

    [Fact]
    public async Task Recalcular_SemVendas_RemoveRecorrencia()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();
        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);

        // Remove todas as vendas (simula exclusão) e recalcula.
        db.Vendas.RemoveRange(db.Vendas);
        await db.SaveChangesAsync();
        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);

        Assert.Empty(await db.RecorrenciasCompra.ToListAsync());
    }

    [Fact]
    public async Task Recalcular_VendaExcluida_NaoConta()
    {
        var (db, clienteId, produtoId) = await CriarBaseAsync();
        var v1 = await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var v2 = await CriarVendaAsync(db, clienteId, produtoId, new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
        v2.Excluir();
        await db.SaveChangesAsync();

        await RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, CancellationToken.None);

        var rec = await db.RecorrenciasCompra.SingleAsync();
        Assert.Equal(45m, rec.IntervaloMedioDias); // só 1 venda vigente → default
        Assert.Equal(1, rec.QuantidadeCompras);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~RecorrenciaCompraAprendizadoTests" -v`
Expected: FAIL — `RecorrenciaCompraAprendizado` inexistente.

- [ ] **Step 3: Implement `RecorrenciaCompraAprendizado`**

```csharp
using AndreGas.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Common;

/// <summary>
/// Recalcula o resumo de recorrência (Cliente×Produto) a partir das vendas vigentes. Chamado
/// sempre que o conjunto de vendas de um par muda (nova venda, edição, exclusão). O intervalo
/// médio é a média dos gaps entre vendas consecutivas (gaps &lt; 7 dias são ignorados); com uma
/// única venda usa <see cref="Produto.DiasPadraoRecompra"/>; sem vendas remove a linha.
/// </summary>
public static class RecorrenciaCompraAprendizado
{
    public const int GapMinimoDias = 7;

    public static async Task RecalcularAsync(
        AppDbContext db, Guid clienteId, Guid produtoId, CancellationToken cancellationToken)
    {
        var vendas = await db.Vendas
            .Where(v => v.ClienteId == clienteId
                && v.ExcluidaEm == null
                && v.Itens.Any(i => i.ProdutoId == produtoId))
            .OrderBy(v => v.DataHora)
            .Select(v => v.DataHora)
            .ToListAsync(cancellationToken);

        var recorr = await db.RecorrenciasCompra
            .FirstOrDefaultAsync(r => r.ClienteId == clienteId && r.ProdutoId == produtoId, cancellationToken);

        if (vendas.Count == 0)
        {
            if (recorr is not null) db.RecorrenciasCompra.Remove(recorr);
            return;
        }

        var defaultDias = (await db.Produtos.FindAsync([produtoId], cancellationToken))?.DiasPadraoRecompra ?? 0;

        decimal intervalo = defaultDias;
        if (vendas.Count >= 2)
        {
            var gapsValidos = new List<double>();
            for (var i = 1; i < vendas.Count; i++)
            {
                var gap = (vendas[i] - vendas[i - 1]).TotalDays;
                if (gap >= GapMinimoDias) gapsValidos.Add(gap);
            }
            // Só usa a média dos gaps se houver ao menos um gap válido; senão volta ao default.
            if (gapsValidos.Count > 0) intervalo = (decimal)gapsValidos.Average();
        }

        // A quantidade é o número de vendas vigentes (não dos gaps).
        if (recorr is null)
        {
            db.RecorrenciasCompra.Add(new RecorrenciaCompra(clienteId, produtoId, vendas[^1], intervalo, vendas.Count));
        }
        else
        {
            recorr.Atualizar(vendas[^1], intervalo, vendas.Count);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~RecorrenciaCompraAprendizadoTests" -v`
Expected: PASS.

- [ ] **Step 5: Add `DbSet` + EF config**

- `AppDbContext.cs`: `public DbSet<RecorrenciaCompra> RecorrenciasCompra => Set<RecorrenciaCompra>();`
- `RecorrenciaCompraConfiguration.cs` (espelha `PrecoPersonalizadoClienteConfiguration`): table `RecorrenciasCompra`, `Id` `ValueGeneratedNever`, `IntervaloMedioDias` `HasPrecision(10,2)`, unique index `(ClienteId, ProdutoId)`, FKs Restrict para Cliente e Produto.

- [ ] **Step 6: Generate migration**

Run: `dotnet-ef migrations add AdicionarRecorrenciaCompra --project src/AndreGas.Infrastructure --startup-project src/AndreGas.Infrastructure -o Migrations`
Expected: novo arquivo `*_AdicionarRecorrenciaCompra.cs` com a tabela `RecorrenciasCompra` + colunas `AvisarRecompra`/`DiasPadraoRecompra` em `Produtos`.

- [ ] **Step 7: Build + test**

Run: `dotnet build && dotnet test tests/AndreGas.UnitTests`
Expected: build limpo (ou só avisos pré-existentes), todos os testes passam.

- [ ] **Step 8: Commit**

```bash
git add src/AndreGas.Infrastructure/AppDbContext.cs src/AndreGas.Infrastructure/Configurations/RecorrenciaCompraConfiguration.cs src/AndreGas.Infrastructure/Migrations/ src/AndreGas.Web/Common/RecorrenciaCompraAprendizado.cs tests/AndreGas.UnitTests/Common/RecorrenciaCompraAprendizadoTests.cs
git commit -m "feat: motor de aprendizado de recorrência + entidade EF (RecorrenciaCompra)"
```

---

### Task 3: Handler de venda chama o motor (Nova Venda + Edição) e Exclusão recalcula

**Files:**
- Modify: `src/AndreGas.Web/Features/Vendas/NovaVenda/RegistrarVendaCommandHandler.cs` (no `Handle`, **após** `SaveChangesAsync`)
- Modify: `src/AndreGas.Web/Features/Admin/Vendas/ExcluirVendaCommandHandler.cs` (**após** `SaveChangesAsync`)
- Modify: `src/AndreGas.Web/Features/Admin/Vendas/EditarVendaCommandHandler.cs` (**após** `SaveChangesAsync`)
- Test: `tests/AndreGas.UnitTests/Features/Vendas/RegistrarVendaCommandHandlerTests.cs`, `tests/AndreGas.UnitTests/Features/Admin/Vendas/ExcluirVendaCommandHandlerTests.cs`, `tests/AndreGas.UnitTests/Features/Admin/Vendas/EditarVendaCommandHandlerTests.cs`

**Interfaces:**
- Consumes: Task 2 — `RecorrenciaCompraAprendizado.RecalcularAsync(db, clienteId, produtoId, ct)`.
- Produces: nenhuma interface nova para tasks posteriores.
- **Timing crítico**: `RecalcularAsync` consulta o banco. A venda recém-criada (ou marcada como excluída) só é visível **após** `SaveChangesAsync`. Portanto o motor é chamado **sempre após o `SaveChangesAsync`** de cada handler, nunca antes nem dentro de `RegistrarAsync`.

- [ ] **Step 1: Write failing test — Nova Venda cria recorrência**

Adicionar à `RegistrarVendaCommandHandlerTests`:

```csharp
[Fact]
public async Task Handle_ProdutoComAvisarRecompra_DeveCriarRecorrencia()
{
    var db = InMemoryDbContextFactory.Create();
    var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
    var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
        avisarRecompra: true, diasPadraoRecompra: 45);
    produto.RegistrarEntrada(10);
    db.Clientes.Add(cliente);
    db.Produtos.Add(produto);
    await db.SaveChangesAsync();

    var handler = new RegistrarVendaCommandHandler(db);
    await handler.Handle(new RegistrarVendaCommand(
        cliente.Id, null, null, null, FormaPagamento.Dinheiro, 0m,
        [new ItemVendaInput(produto.Id, 2)]), CancellationToken.None);

    var rec = Assert.Single(db.RecorrenciasCompra);
    Assert.Equal(cliente.Id, rec.ClienteId);
    Assert.Equal(produto.Id, rec.ProdutoId);
    Assert.Equal(45m, rec.IntervaloMedioDias);
}

[Fact]
public async Task Handle_ProdutoSemAvisarRecompra_DeveIgnorarRecorrencia()
{
    // mesmo setup, produto sem avisarRecompra
    Assert.Empty(db.RecorrenciasCompra);
}
```

(Verificar imports/tipos existentes no arquivo antes; o `RegistrarVendaCommand` pode exigir `TelefoneClienteNovo` nulo quando `ClienteId` presente.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~RegistrarVendaCommandHandlerTests" -v`
Expected: FAIL — `RecorrenciasCompra` vazia.

- [ ] **Step 3: Implement no `Handle` do `RegistrarVendaCommandHandler` (após `SaveChangesAsync`)**

O `Handle` atual:
```csharp
var resultado = await RegistrarAsync(db, command, vendedorId, usuario, cancellationToken);
await db.SaveChangesAsync(cancellationToken);
return resultado;
```

Adicionar **após** `SaveChangesAsync`:

```csharp
// Recalcula a recorrência de recompra dos produtos com aviso (após persistir: o motor consulta o banco).
foreach (var produtoId in command.Itens.Select(i => i.ProdutoId).Distinct())
{
    var produto = await db.Produtos.FindAsync([produtoId], cancellationToken);
    if (produto is { AvisarRecompra: true })
    {
        await RecorrenciaCompraAprendizado.RecalcularAsync(db, vendedorId ?? cliente.Id, produtoId, cancellationToken);
        // ATENÇÃO: cliente.Id pode não estar em escopo aqui; usar o cliente real do resultado.
    }
}
await db.SaveChangesAsync(cancellationToken); // segundo save para persistir a recorrência
```

> **Nota de implementação**: no `Handle`, o `cliente.Id` é o `resultado.ClienteId` (retornado por `RegistrarAsync`). Coletar os `ProdutoId` dos `command.Itens` que são `AvisarRecompra` (via `db.Produtos.FindAsync`) e recalcular cada um. Precisa de um **segundo** `SaveChangesAsync` para persistir as linhas de `RecorrenciasCompra`.

**Edição de venda**: `EditarVendaCommandHandler` marca a venda antiga `Excluir()` e re-cria via `RegistrarAsync` no MESMO `Handle`, com um único `SaveChangesAsync`. Após esse `SaveChangesAsync`, chamar o motor para os produtos da venda antiga (`venda.Itens`) **e** da nova (`command.Itens`) — nesse momento a antiga já está `ExcluidaEm != null` e a nova vigente, então o recálculo dos produtos afetados fica correto. Recalcular também exige um segundo `SaveChangesAsync` ali.

**Exclusão**: no `ExcluirVendaCommandHandler`, após `await db.SaveChangesAsync(cancellationToken);` (que grava `ExcluidaEm`), chamar o motor para cada `item.ProdutoId` com produto `AvisarRecompra`, seguido de outro `SaveChangesAsync`.

> **Motivo do timing**: se o motor rodasse dentro de `RegistrarAsync` (antes do save), a nova venda ainda não estaria visível no banco e o recálculo a ignoraria, mantendo a recorrência obsoleta. O padrão é: **persistir a mutação → recalcular a partir do banco → persistir a recorrência**.

- [ ] **Step 5: Write failing test — Exclusão recalcula**

Adicionar à `ExcluirVendaCommandHandlerTests`:

```csharp
[Fact]
public async Task Handle_ExcluirUltimaVenda_DeveRemoverRecorrencia()
{
    // Setup: cliente + produto avisarRecompra + 1 venda vigente (recorrência criada via handler
    // de venda ou via RecorrenciaCompraAprendizado diretamente).
    // Depois: excluir a venda via ExcluirVendaCommandHandler.
    // Assert: RecorrenciasCompra vazia.
}

[Fact]
public async Task Handle_ExcluirUmaDeDuasVendas_DeveRecalcularIntervalo()
{
    // Setup: 2 vendas com gap 45 → recorrência com intervalo 45 e QuantidadeCompras 2.
    // Excluir a venda MAIS NOVA → recorrência com DataUltimaCompra = antiga, intervalo = default 45, qtd 1.
}
```

(Consultar o `ExcluirVendaCommandHandlerTests` atual para o padrão de setup; o handler exige `venda.Excluir()` e `ReversaoVendaHelper`.)

- [ ] **Step 6: Run test to verify it fails**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~ExcluirVendaCommandHandlerTests" -v`
Expected: FAIL — recorrência não atualizada.

- [ ] **Step 7: Implement no `ExcluirVendaCommandHandler` (após `SaveChangesAsync`)**

Após `venda.Excluir(); await db.SaveChangesAsync(...)` (a exclusão está persistida), para cada `item.ProdutoId` cujo produto é `AvisarRecompra` (via `db.Produtos.FindAsync`):
`await RecorrenciaCompraAprendizado.RecalcularAsync(db, venda.ClienteId, item.ProdutoId, cancellationToken);`
e um segundo `db.SaveChangesAsync(...)`.

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~ExcluirVendaCommandHandlerTests" -v`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/AndreGas.Web/Features/Vendas/NovaVenda/RegistrarVendaCommandHandler.cs src/AndreGas.Web/Features/Admin/Vendas/ExcluirVendaCommandHandler.cs tests/AndreGas.UnitTests/Features/Vendas/RegistrarVendaCommandHandlerTests.cs tests/AndreGas.UnitTests/Features/Admin/Vendas/ExcluirVendaCommandHandlerTests.cs
git commit -m "feat: nova venda e exclusão mantêm a recorrência de compra"
```

---

### Task 4: Home — `ObterDashboardQuery` ganha `Recompras` + dashboard query retorna avisos

**Files:**
- Modify: `src/AndreGas.Web/Features/Home/ObterDashboardQuery.cs` (novo record `RecompraItem` + campo no result)
- Modify: `src/AndreGas.Web/Features/Home/ObterDashboardQueryHandler.cs`
- Test: `tests/AndreGas.UnitTests/Features/Home/ObterDashboardQueryHandlerTests.cs`

**Interfaces:**
- Consumes: Task 1/2 — `RecorrenciaCompra` com `DataUltimaCompra`, `IntervaloMedioDias`, `QuantidadeCompras`; navegação `Cliente`/`Produto`.
- Produces:
  - `RecompraItem(Guid ClienteId, string ClienteNome, string ClienteTelefone, Guid ProdutoId, string ProdutoNome, DateTime DataUltimaCompra, decimal IntervaloMedioDias, DateTime DataPrevista, int DiasDecorridos, int QuantidadeCompras)`
  - `DashboardResult.Recompras` : `IReadOnlyList<RecompraItem>`.

- [ ] **Step 1: Write failing test — query retorna recompras vencidas**

À `ObterDashboardQueryHandlerTests` (FixedNow = 2026-09-15T14:00Z):

```csharp
[Fact]
public async Task Handle_DeveRetornarRecomprasVencidas_NaoRetornarNaoVencidasNemSemIntervalo()
{
    var db = InMemoryDbContextFactory.Create();
    var cliente = new Cliente("Maria Souza", "11999999999", "Rua A, 10");
    var produto = new Produto("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
        avisarRecompra: true, diasPadraoRecompra: 45);
    produto.RegistrarEntrada(10);
    db.Clientes.Add(cliente);
    db.Produtos.Add(produto);

    // Vencida: comprou 01/08 → dataPrevista 15/09 (45d) → hoje 15/09 → aparece.
    var vendaVencida = new Venda(cliente.Id, FormaPagamento.Dinheiro, new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc));
    vendaVencida.AdicionarItem(produto.Id, 1, 100m, 60m);
    db.Vendas.Add(vendaVencida);

    // Ainda não vencida: comprou 01/09 → dataPrevista 16/10 → hoje 15/09 → NÃO aparece.
    var outro = new Cliente("João Pereira", "11988887777", "Rua B, 20");
    var produtoSemVencido = new Produto("Água 20L", TipoProduto.Agua, 25m, 12m, 18m, 10,
        avisarRecompra: true, diasPadraoRecompra: 45);
    produtoSemVencido.RegistrarEntrada(10);
    db.Clientes.Add(outro);
    db.Produtos.Add(produtoSemVencido);
    var vendaNaoVencida = new Venda(outro.Id, FormaPagamento.Dinheiro, new DateTime(2026, 9, 1, 14, 0, 0, DateTimeKind.Utc));
    vendaNaoVencida.AdicionarItem(produtoSemVencido.Id, 1, 25m, 12m);
    db.Vendas.Add(vendaNaoVencida);

    // Intervalo 0 (DiasPadraoRecompra = 0): NÃO aparece mesmo com 1 compra.
    var semIntervalo = new Cliente("Ana Lima", "11977776666", "Rua C, 30");
    var prodSemIntervalo = new Produto("Copo 500ml", TipoProduto.Outros, 2m, 1m, 1.5m, 50,
        avisarRecompra: true, diasPadraoRecompra: 0);
    prodSemIntervalo.RegistrarEntrada(100);
    db.Clientes.Add(semIntervalo);
    db.Produtos.Add(prodSemIntervalo);
    var vendaSemIntervalo = new Venda(semIntervalo.Id, FormaPagamento.Dinheiro, new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc));
    vendaSemIntervalo.AdicionarItem(prodSemIntervalo.Id, 1, 2m, 1m);
    db.Vendas.Add(vendaSemIntervalo);

    await db.SaveChangesAsync();

    var handler = CriarHandler(db, FixedNow);
    var result = await handler.Handle(new ObterDashboardQuery(PeriodoDashboard.Hoje), CancellationToken.None);

    var item = Assert.Single(result.Recompras);
    Assert.Equal(cliente.Id, item.ClienteId);
    Assert.Equal("Maria Souza", item.ClienteNome);
    Assert.Equal("11999999999", item.ClienteTelefone);
    Assert.Equal(produto.Id, item.ProdutoId);
    Assert.Equal("Botijão 13kg", item.ProdutoNome);
    Assert.Equal(45m, item.IntervaloMedioDias);
    Assert.Equal(45, item.DiasDecorridos);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~ObterDashboardQueryHandlerTests" -v`
Expected: FAIL — `DashboardResult.Recompras` inexistente.

- [ ] **Step 3: Implement o record + o campo no result**

- `ObterDashboardQuery.cs`: adicionar `RecompraItem` record + `IReadOnlyList<RecompraItem> Recompras` ao `DashboardResult` (posição no final do record).
- `ObterDashboardQueryHandler.cs`: no `Handle`, consultar:

```csharp
var hojeUtc = timeProvider.GetUtcNow().UtcDateTime;

var recompras = await db.RecorrenciasCompra
    .Where(r => r.IntervaloMedioDias > 0)
    .Include(r => r.Cliente)
    .Include(r => r.Produto)
    .ToListAsync(cancellationToken);

var listaRecompras = recompras
    .Where(r => r.Produto is not null && r.Cliente is not null
        && r.DataUltimaCompra.AddDays((double)r.IntervaloMedioDias) <= hojeUtc
        && hojeUtc <= r.DataUltimaCompra.AddDays((double)r.IntervaloMedioDias * 3)) // janela 2× + folga
    .OrderByDescending(r => r.DataUltimaCompra.AddDays((double)r.IntervaloMedioDias)) // mais atrasado primeiro
    .Take(10)
    .Select(r => new RecompraItem(
        r.Cliente!.Id,
        r.Cliente!.Nome,
        r.Cliente!.Telefone,
        r.Produto!.Id,
        r.Produto!.Nome,
        r.DataUltimaCompra,
        r.IntervaloMedioDias,
        r.DataUltimaCompra.AddDays((double)r.IntervaloMedioDias),
        (int)(hojeUtc - r.DataUltimaCompra).TotalDays,
        r.QuantidadeCompras))
    .ToList();
```

Passar `Recompras: listaRecompras` no `DashboardResult`.

> Nota sobre a **janela**: a spec diz "até `dataPrevista + 2 × IntervaloMedioDias`" = fator 3× a partir da última compra. O código acima usa `AddDays(intervalo * 3)` — se preferir o exposto pela spec (`dataPrevista + 2×intervalo`), use `AddDays((double)r.IntervaloMedioDias * 3)` equivalendo a `dataPrevista + 2·intervalo`. Documentar no comentário.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~ObterDashboardQueryHandlerTests" -v`
Expected: PASS.

- [ ] **Step 5: Add tests for edge cases (Review Focus #1/#3/#4)**

```csharp
[Fact]
public async Task Handle_ProdutoComAvisoRecompraPor_ComProdutoInativo_DeveRetornar()
{
    // produto.Desativar() antes de salvar. Item ainda deve aparecer em Recompras
    // (mesmo sendo inativo — o operador liga para oferecer outro produto).
}

[Fact]
public async Task Handle_RecompraVencida_MasForaDaJanela_NaoDeveRetornar()
{
    // 2 vendas com gap 45, última compra há 150 dias → dataPrevista vencida há muito
    // (fora de `+3×intervalo`) → NÃO aparece (proxy de "desistiu de recomprar").
}

[Fact]
public async Task Handle_RecompraComIntervaloZero_ComMultiplasCompras_DeveIgnorarGapsZero()
{
    // produto avisarRecompra com DiasPadraoRecompra 0 e 2 vendas no mesmo dia (gap 0 ignorado)
    // → IntervaloMedioDias fica 0 → não aparece (sem dados suficientes).
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~ObterDashboardQueryHandlerTests" -v`
Expected: PASS (novos testes).

- [ ] **Step 7: Commit**

```bash
git add src/AndreGas.Web/Features/Home/ObterDashboardQuery.cs src/AndreGas.Web/Features/Home/ObterDashboardQueryHandler.cs tests/AndreGas.UnitTests/Features/Home/ObterDashboardQueryHandlerTests.cs
git commit -m "feat: dashboard retorna recompras vencidas para a home"
```

---

### Task 5: Cadastro/Edição de Produto — UI + command/query

**Files:**
- Modify: `src/AndreGas.Web/Features/Estoque/Cadastrar/CadastrarProdutoCommand.cs`, `CadastrarProdutoCommandValidator.cs`, `CadastrarProdutoCommandHandler.cs`, `ProdutoCadastro.razor`
- Modify: `src/AndreGas.Web/Features/Estoque/Editar/AtualizarProdutoCommand.cs`, `AtualizarProdutoCommandValidator.cs`, `AtualizarProdutoCommandHandler.cs`, `EditarProdutoDialog.razor`, `ObterProdutoQuery.cs`, `ObterProdutoQueryHandler.cs`
- Test: `tests/AndreGas.UnitTests/Features/Estoque/CadastrarProdutoCommandHandlerTests.cs`, `tests/AndreGas.UnitTests/Features/Estoque/AtualizarProdutoCommandHandlerTests.cs`, `CadastrarProdutoCommandValidatorTests.cs`, `AtualizarProdutoCommandValidatorTests.cs`, `ObterProdutoQueryHandlerTests.cs`

**Interfaces:**
- Consumes: Task 1 — `Produto.AvisarRecompra`/`DiasPadraoRecompra` (props + ctor/`AtualizarDados` params).
- Produces:
  - `CadastrarProdutoCommand(..., bool ControlaVasilhame = false, int QuantidadeVasilhamesInicial = 0, bool AvisarRecompra = false, int DiasPadraoRecompra = 0)`
  - `AtualizarProdutoCommand(Guid ProdutoId, string Nome, TipoProduto Tipo, decimal PrecoVenda, decimal PrecoCusto, decimal PrecoGasDoPovo, int EstoqueMinimo, bool Ativo, bool ControlaVasilhame = false, bool AvisarRecompra = false, int DiasPadraoRecompra = 0)`
  - `ObterProdutoResult` ganha `bool AvisarRecompra`, `int DiasPadraoRecompra`.

- [ ] **Step 1: Write failing tests — commands**

`CadastrarProdutoCommandHandlerTests`: novo teste persiste `AvisarRecompra`/`DiasPadraoRecompra` via `CadastrarProdutoCommand`.
`AtualizarProdutoCommandHandlerTests`: novo teste persiste flags via `AtualizarProdutoCommand`.
`ObterProdutoQueryHandlerTests`: novo teste retorna flags.
`CadastrarProdutoCommandValidatorTests`/`AtualizarProdutoCommandValidatorTests`: novo teste rejeita `DiasPadraoRecompra < 0`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~CadastrarProdutoCommandHandlerTests|FullyQualifiedName~AtualizarProdutoCommandHandlerTests|FullyQualifiedName~ObterProdutoQueryHandlerTests|FullyQualifiedName~CadastrarProdutoCommandValidatorTests|FullyQualifiedName~AtualizarProdutoCommandValidatorTests" -v`
Expected: FAIL — flags inexistentes.

- [ ] **Step 3: Implement commands/handlers/validators/query**

Seguir o padrão do vasilhame: adicionar os 2 campos ao command, ao `new Produto(...)`/`AtualizarDados(...)` no handler, ao validator (`RuleFor(x => x.DiasPadraoRecompra).GreaterThanOrEqualTo(0)`) e ao `ObterProdutoQueryHandler` select/record.

- [ ] **Step 4: Run tests to verify they pass**

Run: mesmo comando do Step 2.
Expected: PASS.

- [ ] **Step 5: UI — `ProdutoCadastro.razor`**

Adicionar após o `MudSwitch` do vasilhame:
```razor
<MudSwitch T="bool" @bind-Value="_avisarRecompra" Label="Avisar recompra" Color="Color.Primary" Class="mb-2" />
@if (_avisarRecompra)
{
    <MudNumericField T="int" @bind-Value="_diasPadraoRecompra" Label="Dias padrão para recompra (ex.: 45)" Variant="Variant.Outlined" Class="mb-4" Min="0" />
}
```
E no `@code`: `private bool _avisarRecompra; private int _diasPadraoRecompra;` + incluir no command (default `false`, `0`).

- [ ] **Step 6: UI — `EditarProdutoDialog.razor`**

Mesmos campos, pré-preenchidos em `CarregarAsync` de `_avisarRecompra`/`_diasPadraoRecompra` (vindos do `ObterProdutoResult`) e incluídos no `AtualizarProdutoCommand`.

- [ ] **Step 7: Build + tests**

Run: `dotnet build && dotnet test tests/AndreGas.UnitTests`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AndreGas.Web/Features/Estoque/ src/AndreGas.Web/Features/Estoque/Editar/ tests/AndreGas.UnitTests/Features/Estoque/
git commit -m "feat: cadastro/edição de produto com aviso de recompra"
```

---

### Task 6: SeedDemoData — produtos de demo com `AvisarRecompra` + recorrências coerentes

**Files:**
- Modify: `src/AndreGas.Web/Features/Seed/SeedDemoData.cs`
- Test: `tests/AndreGas.UnitTests/Features/Seed/SeedDemoDataTests.cs`

**Interfaces:**
- Consumes: Task 2 — `RecorrenciaCompraAprendizado.RecalcularAsync`.

- [ ] **Step 1: Write failing test**

`SeedDemoDataTests`: após `SeedAsync`, para cada produto com `AvisarRecompra` marcado no seed, existir `RecorrenciasCompra` para clientes que compraram (ex.: Maria + Botijão 13kg), com `IntervaloMedioDias > 0`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~SeedDemoDataTests" -v`
Expected: FAIL.

- [ ] **Step 3: Implement no seed**

- Marcar no seed: `botijao13` → `avisarRecompra: true, diasPadraoRecompra: 45`, `agua20l` → `avisarRecompra: true, diasPadraoRecompra: 30`, `copoAgua` → `avisarRecompra: true, diasPadraoRecompra: 15`.
- O seed hoje faz `db.Vendas.Add(venda)` e um único `SaveChangesAsync` no final. **Após** esse `SaveChangesAsync` final, chamar `RecorrenciaCompraAprendizado.RecalcularAsync` para cada `(cliente, produto)` com `AvisarRecompra` (coletar dos itens das vendas criadas) e um segundo `SaveChangesAsync`. O motor precisa rodar após persistir as vendas para enxergá-las.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AndreGas.UnitTests --filter "FullyQualifiedName~SeedDemoDataTests" -v`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Web/Features/Seed/SeedDemoData.cs tests/AndreGas.UnitTests/Features/Seed/SeedDemoDataTests.cs
git commit -m "feat: seed com recorrências de recompra para produtos de demonstração"
```

---

### Task 7: UI Home — card "Possível recompra" + ação "Registrar venda"

**Files:**
- Modify: `src/AndreGas.Web/Features/Home/Dashboard.razor`
- Modify: `src/AndreGas.Web/Features/Vendas/NovaVenda/NovaVendaDialog.razor` (param `ClienteInicialId`)
- Test: integração `tests/AndreGas.IntegrationTests/RecompraFeatureTests.cs`

**Interfaces:**
- Consumes: Task 4 — `RecompraItem` no `DashboardResult`; Task 1 — `RecorrenciaCompra`.
- Produces: `NovaVendaDialog` ganha `[Parameter] public Guid? ClienteInicialId`.

- [ ] **Step 1: Implement `NovaVendaDialog` param `ClienteInicialId`**

- Adicionar `[Parameter] public Guid? ClienteInicialId { get; set; }`.
- No `OnInitializedAsync`/`OnAfterRenderAsync`, se `ClienteInicialId` set, buscar o cliente (via `BuscarClientesAsync`/handler existente) e setar `_clienteSelecionado` + `ClienteSelecionadoMudou()`, pré-selecionando no autocomplete. **Não alterar o fluxo manual** quando o param é null.

- [ ] **Step 2: Implement card no `Dashboard.razor`**

No topo do template (antes do grid de métricas), seguindo `ProdutosEstoqueBaixo`:

```razor
<MudGrid>
    <MudItem xs="12">
        <MudPaper Class="pa-4 mb-4">
            <MudText Typo="Typo.h6" Class="mb-2">Possível recompra</MudText>
            <MudText Typo="Typo.caption" Class="mb-2" Color="Color.Secondary">
                Clientes que já passaram da data prevista de nova compra. Ligue e pergunte se precisa.
            </MudText>
            @if (_dashboard!.Recompras.Count == 0)
            {
                <MudText>Nenhuma recompra pendente. 👍</MudText>
            }
            else
            {
                <MudList T="RecompraItem">
                    @foreach (var r in _dashboard.Recompras)
                    {
                        <MudListItem T="RecompraItem">
                            <strong>@r.ClienteNome</strong> — @r.ProdutoNome
                            <MudText Typo="Typo.caption" Color="Color.Secondary">
                                Última compra há @r.DiasDecorridos dia(s) (intervalo ~@((int)r.IntervaloMedioDias) dias) · @r.ClienteTelefone
                            </MudText>
                            <MudButton Size="Size.Small" Color="Color.Primary" Variant="Variant.Filled"
                                       OnClick="@(() => AbrirNovaVendaClienteAsync(r.ClienteId))">
                                Registrar venda
                            </MudButton>
                        </MudListItem>
                    }
                </MudList>
            }
        </MudPaper>
    </MudItem>
</MudGrid>
```

- No `@code`: adicionar
```csharp
private async Task AbrirNovaVendaClienteAsync(Guid clienteId)
{
    var parameters = new DialogParameters<NovaVendaDialog>
    {
        { x => x.ClienteInicialId, clienteId },
    };
    await DialogService.ShowAsync<NovaVendaDialog>("Nova Venda", parameters);
}
```
(usar `DialogService` já injetado; importar `NovaVendaDialog` via `@using AndreGas.Web.Features.Vendas.NovaVenda`).

- [ ] **Step 3: Write failing integration test — `RecompraFeatureTests`**

```csharp
using AndreGas.Domain.Entities;
using AndreGas.Domain.Enums;
using AndreGas.Infrastructure;
using AndreGas.Web.Common;
using AndreGas.Web.Features.Estoque.Cadastrar;
using AndreGas.Web.Features.Home;
using AndreGas.Web.Features.Vendas.NovaVenda;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.IntegrationTests.Tests;

public class RecompraFeatureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AvisoAparece_QuandoDataPrevistaVencida_ESomeAoRegistrarVenda()
    {
        using var db = fixture.CreateDbContext();

        // Cliente + produto com AvisarRecompra: true, DiasPadraoRecompra: 45, com estoque.
        var cliente = new Cliente("Maria Souza", "11911112222", "Rua A, 1");
        var produtoId = await new CadastrarProdutoCommandHandler(db).Handle(
            new CadastrarProdutoCommand("Botijão 13kg", TipoProduto.Gas, 100m, 60m, 80m, 5,
                ControlaVasilhame: true, AvisarRecompra: true, DiasPadraoRecompra: 45),
            CancellationToken.None);
        db.Clientes.Add(cliente);
        var produto = await db.Produtos.FindAsync([produtoId]);
        produto!.RegistrarEntrada(10);
        await db.SaveChangesAsync();

        // Venda 45 dias atrás (via Venda direta + recálculo, pois o comando não expõe DataHora).
        var vendaAntiga = new Venda(cliente.Id, FormaPagamento.Dinheiro, DateTime.UtcNow.AddDays(-45));
        vendaAntiga.AdicionarItem(produtoId, 1, 100m, 60m);
        db.Vendas.Add(vendaAntiga);
        await db.SaveChangesAsync();
        await RecorrenciaCompraAprendizado.RecalcularAsync(db, cliente.Id, produtoId, CancellationToken.None);
        await db.SaveChangesAsync();

        // Dashboard HOJE (TimeProvider real) → recompras contém Maria + produto.
        var dashboardHandler = new ObterDashboardQueryHandler(db, TimeProvider.System);
        var result = await dashboardHandler.Handle(new ObterDashboardQuery(PeriodoDashboard.Tudo), CancellationToken.None);
        Assert.Contains(result.Recompras, r => r.ClienteId == cliente.Id && r.ProdutoId == produtoId);

        // Registrar nova venda HOJE via handler real (produto com estoque) → DataUltimaCompra vira hoje.
        await new RegistrarVendaCommandHandler(db).Handle(
            new RegistrarVendaCommand(cliente.Id, null, null, null, FormaPagamento.Dinheiro, 0m,
                [new ItemVendaInput(produtoId, 1)]),
            CancellationToken.None);

        // Recorrência recalcula: agora última compra = hoje → dataPrevista futura → fora dos avisos.
        var result2 = await dashboardHandler.Handle(new ObterDashboardQuery(PeriodoDashboard.Tudo), CancellationToken.None);
        Assert.DoesNotContain(result2.Recompras, r => r.ClienteId == cliente.Id && r.ProdutoId == produtoId);
    }
}
```

**Atenção — `RegistrarVendaCommand` NÃO expõe `DataHora`** (verificado: tem `ClienteId`, `TelefoneClienteNovo`, `NomeClienteNovo`, `EnderecoClienteNovo`, `FormaPagamento`, `Desconto`, `Itens`, `ValorEntrega` — sem data; a data só entra via parâmetro interno de `RegistrarAsync`). Por isso o teste constrói a venda passada **diretamente** (`new Venda(...)` + recálculo) e usa o handler real apenas para a venda "de hoje". Em tempo de execução, a Nova Venda grava `UtcNow` sozinha.

- [ ] **Step 4: Run integration test**

Run: `dotnet test tests/AndreGas.IntegrationTests --filter "FullyQualifiedName~RecompraFeatureTests" -v`
Expected: PASS (ou ajustes de assinatura conforme indicado).

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Web/Features/Home/Dashboard.razor src/AndreGas.Web/Features/Vendas/NovaVenda/NovaVendaDialog.razor tests/AndreGas.IntegrationTests/RecompraFeatureTests.cs
git commit -m "feat: card de possível recompra na home com ação registrar venda"
```

---

### Task 8: Verificação final + branch/commit

**Files:** nenhum novo — rodar suíte completa.

- [ ] **Step 1: Run all tests**

Run: `dotnet test`
Expected: todos PASS (unit + integração). Se integração travar por volume Postgres, ver gotcha do CLAUDE.md (`docker volume rm andregas.apphost-<hash>-postgres-data`).

- [ ] **Step 2: Build Release**

Run: `dotnet build -c Release`
Expected: build limpo.

- [ ] **Step 3: Review pass**

`git diff --cached` (se algo staged) — verificar: nenhum debug/console.log, nenhum segredo, flags e campos consistentes. Confirmar que todos os `RecompraItem`/`DashboardResult` usam os mesmos nomes/propriedades. Confirmar que a janela/limite/original do spec batem com o código.

- [ ] **Step 4: Commit final (se houver algo residual)**

Conventional commit `feat: ...` ou `chore: ...`.