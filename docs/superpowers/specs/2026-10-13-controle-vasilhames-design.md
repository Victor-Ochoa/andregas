# Controle de Vasilhames — André Gas e Água

Data: 2026-10-13
Status: Aprovado (brainstorming)

## Contexto

A revendedora precisa controlar os **vasilhames vazios** (botijões de gás e galões de água vazios),
que hoje não são rastreados. Um produto pode ter controle de vasilhame (checkbox no cadastro/edição).
Quando marcado, o produto passa a acompanhar três contadores de **vazios**: disponíveis na
revendedora (`QuantidadeVasilhames`), emprestados a clientes (`QuantidadeVasilhamesEmprestados`) e o
**Total de vasilhames** (`Disponíveis + Emprestados`).

## Escopo

- Produto ganha flag `ControlaVasilhame` + contadores de vazios (disponíveis e emprestados).
- Venda: cada item pode ser marcado como **empréstimo** (checkbox "É empréstimo" por item). Se
  marcado, o ego dos vazios fica com o cliente; se não (troca), o vazio volta ao depósito.
- Movimentação manual de estoque: saída devolve o vazio ao depósito; entrada pergunta se deseja
  adicionar vazios automaticamente quando a quantidade informada supera os disponíveis.
- Tela de estoque: seção **Controle de Vasilhames** (só admin) com disponíveis/emprestados/para quem.
- Tela do cliente: exibe saldo de vasilhames por produto + botões registrar empréstimo e devolução.
- Edição de venda: checkbox "É empréstimo" por item + **preservar a DataHora original** ao editar.

## Fora de escopo

- EstoqueBaixo/dashboard continuam olhando apenas `QuantidadeEstoque` (produtos cheios). Vasilhames
  vazios **não** disparam alerta.
- Não há persistência de `Cliente.QuantidadeVasilhamesEmprestados` (calculado da tabela
  `EmprestimoVasilhame`).

## Modelo de dados

### `Produto` (campos novos)

```csharp
public bool ControlaVasilhame { get; private set; }
public int QuantidadeVasilhames { get; private set; }           // vazios na revendedora
public int QuantidadeVasilhamesEmprestados { get; private set; } // vazios com clientes
public int TotalVasilhames => QuantidadeVasilhames + QuantidadeVasilhamesEmprestados; // ignorado no EF
```

- Ctor e `AtualizarDados` passam a gravar `ControlaVasilhame`.
- Métodos de domínio com guardas de negatividade (espelhando `RegistrarEntrada/Saida`):
  - `RegistrarEntradaVasilhame(int)`, `RegistrarSaidaVasilhame(int)` (guard: não negativar),
  - `RegistrarEmprestimoVasilhame(int)` (Emprestados += qtd),
  - `RegistrarDevolucaoVasilhame(int)` (Emprestados −= qtd).

### `ItemVenda` (campo novo)

```csharp
public bool EhEmprestimo { get; private set; } // default false
```

### `EmprestimoVasilhame` (nova entidade — fonte da verdade de "quem/quanto")

```csharp
public class EmprestimoVasilhame
{
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }
    public int Quantidade { get; private set; }
    public DateTime Data { get; private set; }
    public string? Observacao { get; private set; }
    public DateTime? DevolvidoEm { get; private set; }   // null = aberto; preenchido = devolvido
    public int QuantidadeDevolvida { get; private set; } // quanto já devolveu
}
```

- **Saldo em aberto** de um cliente p/ produto = `Quantidade − QuantidadeDevolvida` das linhas
  **não devolvidas** (`DevolvidoEm == null`).
- `Produto.QuantidadeVasilhamesEmprestados` é um **contador operacional ágil** (validação no handler),
  mantido em sincronia com os registros no mesmo handler/transação.

## Regras de negócio

### Nova Venda (troca vs empréstimo)

- Cada item com controle de vasilhame ganha checkbox "É empréstimo" (default desmarcado). Itens de
  produto **sem** controle não exibem o checkbox.
- **Troca** (desmarcado): `Estoque − Qtd`, `Vasilhames + Qtd` (vazio volta ao depósito).
  **Sem** quitar empréstimos pendentes — devolução é tratada fora da venda.
- **Empréstimo** (marcado): `Estoque − Qtd`, `Emprestados + Qtd` (registrado ao cliente via
  `ItemVenda.EhEmprestimo` + um `EmprestimoVasilhame` com `Data = DataHora da venda`).
- Ambos mantêm a movimentação **Saída** e a **venda** (mesmo estoque de produto cheio; preço,
  desconto, entrega, saldo devedor e lucro seguem inalterados).
- **Integridade de estoque de vazios**: `Vasilhames ≥ 0` sempre. Se em **troca** a quantidade pedida
  supera os vasilhames disponíveis e não há empréstimos do cliente para abater, o handler lança
  `InvalidOperationException` com mensagem clara (o operador deve usar "empréstimo" ou o cliente
  deve devolver antes).

### Validação no Validator da Nova Venda

- Para item **Empréstimo**: quantidade ≤ `Estoque + Emprestados do cliente para aquele produto`
  (o cliente pode "pegar emprestado" o que já está com ele — cobre o estorno de devolução).
- Para item **Troca**: quantidade ≤ `Estoque disponível` (regra atual).
- Motivo do empréstimo: o fluxo "cliente devolve 5 e leva 5 emprestados" faria `Estoque < Qtd` no
  momento do checkout em troca; o empréstimo usa a regra do estoque + emprestados.

### Movimentação manual de estoque (Admin)

`RegistrarMovimentacaoEstoqueCommand` ganha `AdicionarVasilhames` (bool, default false) e
`QuantidadeVasilhamesAdicionar` (int, default 0).

- **Entrada**: `Estoque += N`, `Vasilhames += N` (compra traz produto cheio + vasilhame vazio veículo).
  - Se `AdicionarVasilhames == false` → registra movimentação de **Entrada de Vasilhame** com a
    quantidade equivalente (tipo `Vasilhame` no histórico).
  - Se `true` → `Vasilhames += QuantidadeVasilhamesAdicionar` explicitamente.
- **Saída**: `Estoque − N`, `Vasilhames += N` (**vazio volta ao depósito**).
- **Ajuste**: `Estoque = N` **apenas** para o produto cheio, **sem tocar** Vasilhames/Emprestados.

**Prompt na UI (Entrada)**: `MovimentarEstoqueDialog` calcula `QtdEntrada − VasilhamesDisponiveis`
quando positivo → pergunta "Deseja adicionar vasilhames automaticamente?". O admin pode digitar entre
`0` e a diferença — **não** force o máximo (o admin pode ter comprado cheios sem veículos planejados).

### Empréstimos manuais

- **Registrar empréstimo** (tela cliente/estoque): `Emprestados += Qtd` p/ aquele cliente/produto
  (novo `EmprestimoVasilhame` com `DevolvidoEm = null`). Vasilhames disponíveis **não** mudam
  (é só anotar que está com o cliente), mas o **total** sobe. Restrito a Admin.
- **Devolução** (tela cliente/estoque): **apenas** o que está **em aberto** com o cliente
  (`Emprestados do cliente ≥ Qtd`; caso contrário, o handler lança erro
  "Quantidade devolvida não pode exceder os vasilhames emprestados").
  - A devolução roda **3 regras em cascata** no handler, na ordem:
    1. **quita** do `Emprestados` do cliente (sempre),
    2. **se** o produto tem controle de vasilhame **e** há estoque de cheio disponível
       (`Estoque do produto ≥ Qtd`), **re-abastece** o depósito (`Estoque += Qtd devolvida`) — regra
       válida para qualquer devolução,
    3. **senão** (produto com controle mas **sem** estoque de cheio suficiente, ou produto com
       empréstimos pendentes do próprio cliente), **mantém** o estoque de cheio — o sistema consome
       de outros lugares, e o que seria o veículo devolvido não vira cheio "do nada".
  - **Exemplo**: Maria devolve 5 vasilhames de "Botijão Gás 13kg", com `Estoque 2` e sem pendências
    → `Estoque += 5` (7). Se Maria ainda tinha 4 pendências, `Estoque` mantém 2.

### Edição e exclusão de venda (estorno)

- `ReversaoVendaHelper.ReverterAsync` e `EditarVendaCommandHandler` passam a reverter
  **Emprestados/Vasilhames** quando a venda tinha checkbox de empréstimo:
  - reverter **troca**: `Vasilhames − Qtd`, `Estoque + Qtd`,
  - reverter **empréstimo**: `Emprestados do cliente − Qtd`, `Estoque + Qtd`.
- **Edição inclui o checkbox "É empréstimo" por item** (mesmo comportamento da Nova Venda).
- **DataHora preservada**: `EditarVendaCommandHandler` passa `venda.DataHora` original para
  `RegistrarAsync` (nova venda recriada), em vez de `UtcNow` (comportamento atual que trocava a data).

### Auditoria

- Novos tipos no enum `TipoHistoricoEstoque` para eventos de vasilhame: `Vasilhame` (entrada/saída/
  ajuste de vazios no depósito), `EmprestimoVasilhame` (vazio emprestado ao cliente) e
  `DevolucaoVasilhame` (vazio devolvido pelo cliente). Cada qual com descrição e usuário (quem).
- `EstoqueLista.razor`: nova tabela **Controle de Vasilhames** (restrita a Admin) listando apenas
  produtos com `ControlaVasilhame`:
  - Colunas: Produto | Disponível (depósito) | Emprestados | **Total** (`Disponível + Emprestados`).
  - Linha clicável → modal **Vasilhames Emprestados** (quem/quanto, vinda de `EmprestimoVasilhame`).
  - Botões: Registrar devolução, Registrar empréstimo.
- `ClienteDetalhe.razor`: exibe saldo de vasilhames por produto + botões registrar empréstimo e
  devolução.

## Migração EF (nova): `AdicionarControleVasilhames`

### Colunas novas

| Tabela | Coluna | Tipo | Default |
|---|---|---|---|
| Produtos | `ControlaVasilhame` | bool | false |
| Produtos | `QuantidadeVasilhames` | int | 0 |
| Produtos | `QuantidadeVasilhamesEmprestados` | int | 0 |
| ItensVenda | `EhEmprestimo` | bool | not null, default false |

### Nova tabela `EmprestimosVasilhames`

| Coluna | Tipo |
|---|---|
| Id | Guid PK (ValueGeneratedNever) |
| ClienteId | Guid FK → Clientes (Restrict) |
| ProdutoId | Guid FK → Produtos (Restrict) |
| Quantidade | int |
| Data | timestamptz |
| Observacao | string? |
| DevolvidoEm | timestamptz? |
| QuantidadeDevolvida | int default 0 |

### Backfill

- Migration com `UPDATE`/`INSERT` para produtos de tipo movimentado (Botijão Gás 13kg, 45kg,
  Galão Água 20L) com `ControlaVasilhame = true`, **sem** mexer nos contadores (sem histórico
  confiável de vazios).
- Seed `SeedDemoData` idem.

## Handlers & Queries (novos)

- `RegistrarEmprestimoCommand` / Handler — empréstimo manual.
- `RegistrarDevolucaoVasilhameCommand` / Handler — cascata das 3 regras da devolução.
- `ObterControleVasilhamesQuery` / Handler — lista agregada (estoque).
- `ObterEmprestimosClienteQuery` / Handler — lista empréstimos do cliente.
- `ObterProdutoQuery`/result: adicionar `ControlaVasilhame`, `QuantidadeVasilhames`,
  `QuantidadeVasilhamesEmprestados`.
- `ListarProdutosQuery`/item: adicionar esses 3 + `TotalVasilhames`.
- `RegistrarVendaCommand`: `ItemVendaInput` ganha `bool EhEmprestimo = false`.
- `EditarVendaCommand`/`ItemVendaInput`: idem (plus `DataHora` preservada no handler).
- `RegistrarMovimentacaoEstoqueCommand`: novos `AdicionarVasilhames`, `QuantidadeVasilhamesAdicionar`.
- `ObterClienteDetalheQuery`: resultado ganha saldo de vasilhames (total + por produto).
- `ListarClientesQuery`/item: ganha `QuantidadeVasilhamesEmprestados` (coluna opcional na UI).

## UI (componentes)

- `ProdutoCadastro.razor` & `EditarProdutoDialog.razor`: checkbox "Controle de vasilhames" +
  (edição) exibe Total de vasilhames (read-only).
- `NovaVendaDialog.razor`: checkbox "É empréstimo" por item condicional (só `ControlaVasilhame`).
- `MovimentarEstoqueDialog.razor`: prompt de vasilhames (seção acima).
- `EstoqueLista.razor`: seção "Controle de Vasilhames" + modal "Vasilhames Emprestados".
- `ClienteDetalhe.razor`: saldo de vasilhames + registrar empréstimo/devolução.
- `Admin/Vendas/EditarVendaDialog.razor`: checkbox "É empréstimo" por item; data original preservada
  (não editável no dialog — campo somente leitura ou oculto).

## Testes

Unit (InMemory):
- `ProdutoTests`: novos métodos de vasilhame + guardas.
- `RegistrarVendaCommandHandlerTests`: troca vs empréstimo com o mesmo produto; estorno; estorno de
  venda de empréstimo (Emprestados−, Vasilhames+); estorno de troca.
- `RegistrarMovimentacaoEstoqueCommandHandlerTests`: entrada com `AdicionarVasilhames`; entrada s/
  flag; saída volta vazio.
- `RegistrarEmprestimo*`, `RegistrarDevolucaoVasilhame*` isolados.
- `ListarProdutos/ObterProduto/ObterClienteDetalhe` novos campos.
- `EditarVenda` preserva `DataHora` original.

Integração (Postgres via `DatabaseFixture`):
- `VasilhamesFeatureTests` (novo): venda com empréstimo, devolução, estorno, dados de "para quem".

## Decisões registradas

1. Checkbox "É empréstimo" por item, também na **edição** de venda.
2. **Preservar a `DataHora` original** da venda ao editar (corrige bug atual).
3. Troca: `Estoque −`, `Vasilhames +`; Empréstimo: `Estoque −`, `Emprestados +`.
4. Devolução: manual (tela do cliente), não pela venda.
5. Entrada com prompt: adicionar **só a diferença** (0..diferença).
6. Saída manual: vazio volta ao depósito (`Vasilhames +`).
7. Fonte da verdade "para quem": tabela `EmprestimoVasilhame`.
8. `EstoqueBaixo`/dashboard continuam olhando só `QuantidadeEstoque` (cheios). Vasilhames não
   disparam alerta.