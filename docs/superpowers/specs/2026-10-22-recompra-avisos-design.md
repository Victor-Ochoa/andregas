# Aviso de recompra na Home — André Gas e Água

Data: 2026-10-22
Status: Rascunho (brainstorming)

## Contexto

Para evitar que o cliente fique sem o produto (gás de cozinha / água mineral), a revendedora quer
avisar **na Home** (quadro superior) quando um cliente provavelmente precisa **recomprar** um
produto, para o operador ligar e perguntar (futuramente, um bot no WhatsApp).

O intervalo de recompra varia por cliente, então o sistema deve **aprender** o padrão real de cada
cliente×produto. Nem todo produto participa — é necessário um **check** no produto, como já existe
para `ControlaVasilhame`.

Decisão de arquitetura: **cálculo incremental, sem worker**. O resumo (`RecorrenciaCompra`) é
mantido **na hora da venda**; a Home apenas **lê** uma query indexada sobre uma tabela pequena.
Custo da Home é desprezível perto do que `ObterDashboardQueryHandler` já carrega hoje (todas as
vendas do período em memória a cada abertura).

## Escopo

- `Produto` ganha `AvisarRecompra` (bool) + `DiasPadraoRecompra` (int, default inicial por produto).
- Nova entidade `RecorrenciaCompra` (Cliente×Produto): última compra, intervalo médio aprendido,
  quantidade de compras consideradas.
- Aprendizado **derivado das vendas vigentes**: criada/editada/excluída venda, a `RecorrenciaCompra`
  dos produtos afetados é **recalculada a partir das vendas não excluídas** do cliente×produto.
  (Sem média móvel frágil; recalcular do zero é barato — poucas vendas por par.)
- Home: novo card **"Possível recompra"** (topo), listando cliente — produto, dias desde a última
  compra, intervalo aprendido e telefone, com ação **"Registrar venda"** que abre a `NovaVendaDialog`
  pré-selecionada com o cliente.
- Cadastro/edição de produto: check "Avisar recompra" + campo "Dias padrão para recompra" (só se
  marcado), espelhando o padrão do vasilhame.

## Fora de escopo

- **Bot no WhatsApp**: fora; o card expõe o telefone (pronto para futura ação).
- **Flag/worker noturno**: descartado na decisão de arquitetura (staleness 24h + ponto de falha se a
  máquina estiver desligada).
- **Configuração manual do intervalo por cliente**: não; o sistema aprende. O default do produto cobre
  o caso de dados insuficientes.
- **Notificação proativa** (push/sms): fora.
- **Fiado/forma de pagamento** não influenciam o aviso (toda venda não excluída conta).

## Modelo de dados

### `Produto` (campos novos)

```csharp
public bool AvisarRecompra { get; private set; }   // default false
public int DiasPadraoRecompra { get; private set; } // default 0
```

- Ctor e `AtualizarDados` ganham parâmetros com default (`false`, `0`).
- Guarda: `DiasPadraoRecompra >= 0`.
- Se `AvisarRecompra == false`, `DiasPadraoRecompra` não é usado (pode ficar `0`).

### `RecorrenciaCompra` (nova entidade — resumo incremental)

```csharp
public class RecorrenciaCompra
{
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }   // FK Cliente
    public Guid ProdutoId { get; private set; }   // FK Produto
    public DateTime DataUltimaCompra { get; private set; }
    public decimal IntervaloMedioDias { get; private set; } // aprendido (ou default do produto)
    public int QuantidadeCompras { get; private set; }     // nº de vendas vigentes consideradas
}
```

- Chave única `(ClienteId, ProdutoId)` — índice FK para lookup rápido na Home.
- Sem coluna de "previsto": `DataPrevista = DataUltimaCompra + IntervaloMedioDias`, derivada.

## Regras de negócio

### Aprendizado (motor reutilizável)

Para cada produto com `AvisarRecompra` presente em uma venda **recém criada/editada**, e para cada
produto de uma venda **excluída**:

1. Buscar **todas as vendas vigentes** (`ExcluidaEm == null`) do `(ClienteId, ProdutoId)`,
   ordenadas por `DataHora`. Todo item de venda vigente conta como compra do produto
   (independe de forma de pagamento ou `EhEmprestimo`); devolução manual de vasilhame não é venda
   e não entra.
2. Se não houver vendas → **remover** a `RecorrenciaCompra` (não existe mais recorrência).
3. Se houver **1 venda** → `IntervaloMedioDias = Produto.DiasPadraoRecompra` (default; 0 se não
   configurado), `DataUltimaCompra = venda`, `QuantidadeCompras = 1`.
4. Se houver **2+ vendas** → `IntervaloMedioDias = média dos gaps` entre vendas consecutivas,
   `DataUltimaCompra = última`, `QuantidadeCompras = n`.

- **Gap mínimo de 7 dias**: gaps < 7 dias são ignorados (trocas rápidas/empréstimos não são ciclos
  de consumo) — se todos os gaps caírem abaixo do mínimo, `QuantidadeCompras` efetiva trata como
  padrão do produto.
- O motor é uma classe estática reutilizável (padrão `ReversaoVendaHelper`), chamada por
  `RegistrarVendaCommandHandler.RegistrarAsync` (cobre Nova Venda **e** Edição), por
  `ExcluirVendaCommandHandler` **e** pelo `SeedDemoData` — consistência garantida.

### Decisão de exibição na Home

- **Base**: `RecorrenciaCompra` com `QuantidadeCompras >= 1` e `IntervaloMedioDias > 0`.
- **Mostra se**: `DataUltimaCompra + IntervaloMedioDias <= hoje` (data prevista vencida). O "hoje" é o
  **dia civil de Brasília (UTC−3)**: a última compra e o momento atual são convertidos para
  `America/Sao_Paulo` e comparados em dias de calendário — o aviso vira a partir da meia-noite
  local, não da meia-noite UTC.
- **Janela de expiração**: mostra apenas até `dataPrevista + 2 × IntervaloMedioDias`. Cliente que
  não recomprou em mais de 2 ciclos sai da lista (deixa de ser "recompra próxima"). Sem isso, a
  lista acumularia clientes para sempre.
- **Ordem**: por atraso — mais atrasado primeiro: `DataPrevista` mais antiga (maior tempo desde a data prevista) primeiro.
- **Limite**: top 10 por abertura (já é o padrão de `TopProdutos`).

### Integração com o fluxo de vendas

- **Nova venda**: `RegistrarAsync` recalcula a `RecorrenciaCompra` dos produtos `AvisarRecompra`
  da venda (mesmo `SaveChangesAsync`). O trigger do recálculo é único: sempre que o **conjunto de
  vendas vigentes** de um (ClienteId, ProdutoId) muda — nova venda, edição ou exclusão — o motor
  `RecalcularAsync` roda para aquele par. Vendas de produtos **sem** `AvisarRecompra` não tocam a
  tabela.
- **Edição**: recria a venda via `RegistrarAsync`; os produtos da **venda antiga** (excluída →
  recalculada) e da **nova** (recalculada) são disparados. A `DataHora` preservada (memória
  `editar-venda-troca-data-original`) garante gaps corretos.
- **Exclusão**: `ExcluirVendaCommandHandler` recalcula os produtos da venda excluída (removendo a
  `RecorrenciaCompra` se não sobrar venda).
- **Seed**: produtos de demo ganham `AvisarRecompra = true` + `DiasPadraoRecompra`; o seed chama o
  motor após criar as vendas, gerando recompras coerentes com as datas já espaçadas.

## Migração EF (nova): `AdicionarRecorrenciaCompra`

### Colunas novas em `Produtos`

| Coluna | Tipo | Default |
|---|---|---|
| `AvisarRecompra` | bool | false |
| `DiasPadraoRecompra` | int | 0 |

### Nova tabela `RecorrenciasCompra`

| Coluna | Tipo |
|---|---|
| Id | Guid PK (ValueGeneratedNever) |
| ClienteId | Guid FK → Clientes (Restrict) |
| ProdutoId | Guid FK → Produtos (Restrict) |
| DataUltimaCompra | timestamptz |
| IntervaloMedioDias | numeric(10,2) |
| QuantidadeCompras | int |

- Índice único `(ClienteId, ProdutoId)`.
- **Backfill**: para produtos `AvisarRecompra = true` (nenhum ainda), nenhum dado a migrar; a tabela
  nasce vazia e aprende com as próximas vendas. Produtos de demo são marcados no `SeedDemoData`.

## Handlers & Queries

- `RecorrenciaCompraAprendizado` (helper estático, novo): `RecalcularAsync(db, clienteId, produtoId, ct)`.
- `RegistrarVendaCommandHandler`: chama o motor para itens `AvisarRecompra` (dentro de `RegistrarAsync`).
- `ExcluirVendaCommandHandler`: chama o motor para os produtos da venda excluída.
- `ObterDashboardQuery`/result: novo `IReadOnlyList<RecompraItem> Recompras`.
- `ObterDashboardQueryHandler`: consulta `RecorrenciasCompra` (join Cliente + Produto) com a condição
  de exibição e janela acima; top 10 ordenado por atraso.
- `ObterProdutoQuery`/result e `ListarProdutosQuery`/item: adicionar `AvisarRecompra`,
  `DiasPadraoRecompra` (para a edição exibir e resetar).
- `CadastrarProdutoCommand` e `AtualizarProdutoCommand`: novos campos.
- `NovaVendaDialog`: novo `[Parameter] Guid? ClienteInicialId` — pré-seleciona o cliente no
  autocomplete (`OnInitialized`), sem alterar o fluxo manual (quando vazio).

## UI (componentes)

- `ProdutoCadastro.razor`: check "Avisar recompra" + (se marcado) `MudNumericField` "Dias padrão para
  recompra (ex.: 45)".
- `EditarProdutoDialog.razor`: mesmos campos, pré-preenchidos (+ reset possível ao desmarcar).
- `Dashboard.razor`: novo card **"Possível recompra"** no topo (acima do grid de métricas), seguindo
  o padrão visual de `ProdutosEstoqueBaixo` (MudPaper + lista). Cada item:
  - **Cliente — Produto**, `Dias desde a última compra`, `Intervalo (ex.: ~45 dias)`, telefone;
  - ação **"Registrar venda"** → `DialogService.ShowAsync<NovaVendaDialog>(ClienteInicialId = id)`.
  - Vazio → "Nenhuma recompra pendente. 👍".
  - Refresh reativo: o card é parte do `ObterDashboardQuery`, já atualizado via `VendasNotifier`
    (some naturalmente ao registrar venda).
- Controle: texto no card "Ligar para o cliente e perguntar se precisa" (orientação ao operador).

## Testes

Unit (InMemory):
- `RecorrenciaCompraAprendizadoTests`:
  - 1 venda → `IntervaloMedioDias = DiasPadraoRecompra`, `QuantidadeCompras = 1`.
  - 2 vendas com gap 45 → média 45; 3 vendas com gaps (45, 30) → média 37.5.
  - Gap < 7 dias ignorado; default do produto quando sem gaps válidos.
  - Sem vendas vigentes → Remove (recorrência some).
  - Venda excluída não conta.
- `ObterRecomprasQueryTests` (ou via dashboard result):
  - Aparece quando `hoje >= dataPrevista`.
  - Não aparece antes da data prevista.
  - Some após `dataPrevista + 2×intervalo` (janela).
  - `QuantidadeCompras`/`IntervaloMedioDias` inválidos (0) → não aparece.
  - Ordenação por atraso + limite 10.
- `RegistrarVendaCommandHandlerTests`: venda cria/atualiza recorrência; **edição** mantém
  consistência (antiga excluída não duplica).
- `Cadastrar/AtualizarProduto`: persiste flags; guarda de negatividade em `DiasPadraoRecompra`.

Integração (Postgres via `DatabaseFixture`):
- `RecompraFeatureTests` (novo): venda → aviso na home; nova venda → some/atualiza; excluir venda →
  recorrência recalculada/removida; cliente sem recorrência não aparece.

## Decisões registradas

1. **Incremental, sem worker** — resumo mantido na venda; Home lê query indexada. (Decisão do dev,
   registrada após análise: custo da Home irrelevante perto do que o dashboard já carrega.)
2. **Aprendizado derivado das vendas vigentes** (recalculável), não média móvel persistida — edição e
   exclusão não corrompem o histórico; motor único reutilizável (handler de venda, exclusão, seed).
3. **1 compra basta** para aparecer — usando `DiasPadraoRecompra` do produto como intervalo inicial.
4. **Aviso a partir da data prevista** (`ultima + intervalo`), com **janela de expiração de 2×
   intervalo** e **limite de 10** itens.
5. **Gap mínimo de 7 dias** não conta como ciclo de consumo.
6. "Registrar venda" abre a Nova Venda **pré-selecionada** com o cliente (`ClienteInicialId`). Sem
   persistir "já liguei" (fora de escopo; nova venda limpa o aviso naturalmente).
7. Produto **inativo** com `AvisarRecompra` continua listado (cliente pode precisar), mas não pode
   ser vendido — a Home orienta ligar.