# Design — Migração para Multi-Empresa (Multi-Tenancy)

- **Data:** 2026-10-27
- **Status:** rascunho para revisão
- **Produto:** Revenda.App (substitui a marca fixa "André Gas e Água" nas telas)

## 1. Objetivo

Transformar o sistema de empresa única "André Gas e Água" em uma aplicação multi-empresa
(Revenda.App), onde:

- O **login identifica a empresa** pelo e-mail do usuário (sem campo extra de empresa).
- Os **dados são isolados por empresa** (coluna `EmpresaId` + filtro global no EF Core).
- O **super admin** loga e cai numa **central** onde configura novas empresas, usuários,
  e pode **entrar como (impersonation)** um admin da empresa — com badge e volta à central.
- O **app muda conforme a empresa**: nome, cor e logo da empresa são exibidos; a tela de
  login é sempre **Revenda.App**.
- Todos os **dados de domínio mestres** (Cliente, Produto, Venda) têm **exclusão lógica**.

## 2. Decisões acordadas (resumo)

| Tema | Decisão |
|---|---|
| Identificação da empresa no login | E-mail define a empresa (1 usuário = 1 empresa) |
| Acesso do super admin à empresa | Entrar como (impersonation) com badge e botão voltar |
| Nome do produto | **Revenda.App** (marca fixa nas telas) |
| Dados atuais na migração | Começar do zero (sem backfill de dados legados) |
| Isolamento | Coluna `EmpresaId` + filtro global EF Core (1 banco/schema) |
| Config por empresa | Nome, identificador, CNPJ, endereço estruturado, telefones, cor, logo, usuários, inativação, auditoria |
| Admin da empresa configura a própria empresa | Sim — aba "Minha Empresa" em `/configuracoes` |
| Primeiro admin | Criado junto com a empresa pelo super admin |
| E-mail de login | E-mail normal do usuário (único no sistema) |
| Super admin | Usuário distinto da central (sem empresa; cai em `/central`) |
| Exclusão lógica | Apenas nas entidades mestre (Cliente, Produto, Venda) |
| Super admin opera produtos | Central = leitura; escrita pelo "Entrar como" |

## 3. Modelo de dados

### 3.1 Nova entidade `Empresa` (Domain, tabela `Empresas`)
| Campo | Tipo | Observações |
|---|---|---|
| `Id` | `Guid` | PK, `ValueGeneratedNever` |
| `Identificador` | `string` | slug amigável; **único** e **imutável** após criação. Ex.: `andre`, `demo` |
| `Nome` | `string` | nome exibido no app bar da empresa |
| `Cnpj` | `string?` | CNPJ da empresa (opcional) |
| `Endereco` | `string?` | logradouro |
| `Numero` | `string?` | número |
| `Cep` | `string?` | CEP |
| `Bairro` | `string?` | bairro |
| `Cidade` | `string?` | cidade |
| `Estado` | `string?` | UF — **será usado em features futuras** (ex.: entrega, geolocalização) |
| `Telefones` | `List<string>` | lista de telefones de contato (cada um normalizado p/ dígitos) |
| `CorPrimaria` | `string?` | hex da cor da empresa (ex.: `#0A1F44`); `null` = azul marinho atual |
| `Logo` | `string?` | imagem (base64 data-uri), opcional, limite ~500KB |
| `Ativo` | `bool` | default `true`; empresa inativa não loga |
| `CriadaEm` | `DateTime` | |

Métodos de domínio: `Ativar()`, `Desativar()`, `AtualizarDados(...)` (nome, CNPJ, endereço,
telefones, cor, logo) — **nunca** muda `Identificador`.

### 3.2 `ApplicationUser` (Identity)
- Ganha `Guid? EmpresaId` (FK → `Empresas`, nullable; **`null` = usuário da central**).
- Ganha `bool Ativo` (default `true`).
- Role nova **`SuperAdmin`** (constante em `ApplicationRole`, criada no seed).
- `IdentityUser` mantém `RequireUniqueEmail = true` — **1 usuário = 1 empresa** (o e-mail
  define a empresa). Sem sufixo no e-mail.

### 3.3 Tabelas de domínio — coluna `EmpresaId`
Todas as tabelas de domínio ganham `Guid EmpresaId` (**NOT NULL**, `private set`,
preenchido pelo interceptor — ver §4).

`Clientes, Produtos, Vendas, ItensVenda, Pagamentos, MovimentacoesEstoque,
HistoricosEstoque, PrecosPersonalizadosClientes, EmprestimosVasilhames,
RecorrenciasCompra`

- `Empresas` e as tabelas do Identity **não** têm `EmpresaId` nem filtro.
- Índices em `EmpresaId` em cada tabela de domínio.
- `Cliente.Telefone` deixa de ser único **global** e passa a ser único **por empresa**
  (índice composto `(EmpresaId, Telefone)`).

### 3.4 Exclusão lógica (soft delete) — apenas entidades mestre
| Entidade | Estado atual | Após |
|---|---|---|
| `Cliente` | já tem `Ativo` | mantém; queries padrão só enxergam ativos quando aplicável |
| `Produto` | já tem `Ativo` | mantém; "excluir produto" = `Desativar()` |
| `Venda` | já tem `ExcluidaEm` | mantém (exclusão lógica já implementada) |
| `ItemVenda`, `Pagamento`, `MovimentacaoEstoque`, `HistoricoEstoque`, `PrecoPersonalizadoCliente`, `EmprestimoVasilhame`, `RecorrenciaCompra` | sem flag | **sem alteração** — removidas fisicamente apenas em reversões/reconciliações automáticas internas (não são excluídas pela UI) |

- "Excluir produto": hoje o produto não tem comando de exclusão na UI (só `Desativar`). O
  super admin "exclui produto" = **desativar** (exclusão lógica), mantendo histórico/estoque.
- Não criar comando de exclusão física adicional.

### 3.5 Auditoria — nova entidade `RegistroAuditoria` (global)
| Campo | Tipo |
|---|---|
| `Id` | `Guid` |
| `EmpresaId` | `Guid?` (null = ação na central) |
| `UsuarioId` | `Guid?` |
| `UsuarioNome` | `string` |
| `Acao` | `string`/enum — ex.: `EmpresaCriada`, `EmpresaEditada`, `EmpresaInativada`, `EmpresaReativada`, `UsuarioCriado`, `UsuarioDesativado`, `EntrarComo`, `ConfigEmpresaEditada` |
| `Detalhes` | `string` (JSON leve ou texto) |
| `DataHora` | `DateTime` |
| `Origem` | `string` — `Central` ou `App` |

## 4. Isolamento por empresa (multi-tenancy)

### 4.1 `ITenantContext` (scoped)
```csharp
public interface ITenantContext
{
    Guid? EmpresaId { get; }          // null = central (super admin)
    string NomeEmpresa { get; }       // nome para exibição
    string CorPrimaria { get; }       // hex
    bool EhSuperAdmin { get; }
    bool Impersonando { get; }
    Guid EmpresaIdRequerida(string acao); // lança se tentar operar domínio sem empresa
}
```
- Implementação lê as **claims** do principal autenticado (funciona em SSR e no circuito
  interativo via `AuthenticationStateProvider`).
- Logo **não** vai ao cookie (base64 pesado) — a tela que precisa consulta o banco com
  cache scoped.

### 4.2 Filtro global dinâmico por instância
- No `OnModelCreating`, para cada tabela de domínio:
  ```csharp
  builder.Entity<Cliente>()
      .HasQueryFilter(c => _empresaId == null || c.EmpresaId == _empresaId);
  ```
- `_empresaId` (Guid?) é injetado no construtor do `AppDbContext` via `ITenantContext`.
- **`null` = modo central** → vê tudo (super admin, seeds, testes, reset). Instâncias com
  empresa filtram por ela.
- **Importante (sem problema de cache de model):** a expressão de filtro é a mesma
  sintática para todas as instâncias; o EF avalia `_empresaId` como **parâmetro por
  query** (closure sobre campo readonly), não no construtor do model — um único model
  cacheado serve para todos os tenants.
- Central nunca roda handlers de domínio: handlers exigem `EmpresaIdRequerida()`.

### 4.3 `SetEmpresaIdInterceptor` (SaveChanges)
- Em entidades `Added` com `EmpresaId == default`, preenche com
  `ITenantContext.EmpresaId` (lança se tentar adicionar linha de domínio sem tenant).
- Handlers **não** setam `EmpresaId` na mão — camada única de tenancy.

### 4.4 Segurança em camadas
- `EmpresaId` NOT NULL em todas as tabelas de domínio — não existe linha "órfã".
- Handlers de domínio chamam `EmpresaIdRequerida()` no início (defensivo).
- Testes de integração de **isolamento explícito** (empresa A não vê/alterar dados de B,
  mesmo telefone/produto separados).
- A central tem política própria (`ApenasSuperAdmin`) e nunca executa handlers de domínio.

## 5. Autenticação

### 5.1 Claims
- `IUserClaimsPrincipalFactory<ApplicationUser>` custom adiciona ao principal:
  `EmpresaId`, `EmpresaNome`, `EmpresaCor`, `EhSuperAdmin`; role `SuperAdmin` via Identity.
- `ITenantContext` lê essas claims.

### 5.2 Login
- `LoginCommandHandler` carrega o usuário por e-mail; antes do `PasswordSignInAsync`
  verifica:
  - `usuario.Ativo` → senão "Usuário desativado. Fale com o administrador."
  - se `usuario.EmpresaId` não-nulo, empresa `Ativo` → senão "Empresa desativada. Fale com o administrador."
  - (sem `EmpresaId` = super admin da central; só valida `Ativo` do usuário).
- Após login bem-sucedido: super admin (sem `EmpresaId`) → redireciona para `/central`;
  usuário de empresa → `/` (app).
- Como a tela de login é SSR estática, o `LoginResult` passa a indicar o **destino**
  (`Destino` = `/central` quando super admin, senão `/`), decidido no `LoginCommandHandler`
  com base no `EmpresaId` do usuário — o `Login.razor` apenas navega para onde o resultado manda.
- **Tela de login é sempre Revenda.App** — sem campo de empresa, sem branding da empresa.
  A marca fixa substitui o "André Gas e Água" atual.

### 5.3 Impersonation ("Entrar como")
- Na central, o super admin escolhe um **admin ativo** da empresa (se houver mais de um,
  mini-seletor) e clica **"Entrar como"**.
- Handler de impersonation faz `SignInAsync` com o principal do **admin alvo** +
  claims extras `Impersonando=true`, `SuperAdminOriginalId`.
- O usuário passa a agir com a role/papel do admin da empresa; o app exibe
  badge **"Visão de super admin"** (em `TopMenuActions`) + botão **"Voltar à central"**
  (faz sign-in de volta como o super admin).
- Impersonation **não** altera `EmpresaId` do super admin no banco — só a sessão.

## 6. UI e branding dinâmico

- **Login sempre Revenda.App** (marca fixa) — sem avatar da empresa, sem empresa na tela.
- **App bar (`MainLayout`) sem avatar**: se a empresa tem logo → logo + nome da empresa;
  senão → só o nome da empresa. Cor da empresa aplicada ao app bar/drawer.
- `PageTitle` de todas as telas → `{Tela} — {NomeEmpresa}` (central: `{Tela} — Revenda.App`).
- `TopMenuActions`: mantém "Configurações"; quando `Impersonando`, mostra badge
  "Visão de super admin" + "Voltar à central".
- Novo serviço scoped `IdentidadeEmpresaService`: `NomeEmpresa`, `CorPrimaria`, `Logo`
  (da claims; logo do banco com cache scoped).
- Tema: `RevendaAppTheme.ComCorPrimaria(hex)` clona o tema com o primary trocado;
  `MudThemeProvider` do `MainLayout` usa a cor da empresa.

## 7. Novas telas

### 7.1 Central do super admin (`/central`)
- Rota protegida por política `ApenasSuperAdmin`.
- **Lista de empresas**: busca/paginação, status (Ativa/Inativa), contagem de usuários.
- **Ações por empresa**:
  - **Entrar como** (impersonation como admin ativo da empresa).
  - **Configurar** — editar nome, identificador (imutável — exibido mas não editável),
    CNPJ, endereço estruturado (logradouro, número, CEP, bairro, cidade, estado),
    telefones, cor, logo.
  - **Usuários** — listar/criar/inativar usuários da empresa (Admin/Vendedor).
  - **Ver produtos** — **leitura** (lista de produtos da empresa); escrita via "Entrar como".
  - **Inativar/Reativar** (confirmado).
- **Histórico de auditoria** (tab): ações da central e por empresa.

### 7.2 Aba "Minha Empresa" (`/configuracoes`)
- Nova aba ao lado de Usuários/Vendas.
- Admin da empresa edita: nome, CNPJ, endereço estruturado, telefones, cor, logo.
- **Não** pode: alterar `Identificador` (imutável), inativar a própria empresa (só super admin).
- Grava `RegistroAuditoria` (`ConfigEmpresaEditada`).

### 7.3 Autorização / redirecionamento
- `RoleAwareNotAuthorized`: super admin tentando abrir tela de domínio sem impersonation → `/central`;
  admin de empresa tentando `/central` → `/sem-autorizacao`.
- Política `ApenasSuperAdmin` (`SuperAdmin` role).
- `Politicas.ApenasAdmin` continua (Admin da empresa).

## 8. Migração, seed e testes

### 8.1 Migração EF Core (única)
- Tabela `Empresas`; `ApplicationUser.EmpresaId` + `Ativo`; `EmpresaId` nas tabelas de
  domínio; índice composto único `(EmpresaId, Telefone)` em Clientes; índices `EmpresaId`
  nas demais; role `SuperAdmin` (seed — não é migration).
- **Começar do zero**: sem script de backfill. O banco nasce limpo; o super admin cadastra
  as empresas pela central.

### 8.2 Seed / bootstrap
- `SeedData`:
  - Cria role `SuperAdmin` (junto com `Admin`/`Vendedor`).
  - Cria **SuperAdmin** da central (sem `EmpresaId`, role `SuperAdmin`), credenciais via
    `SeedAdmin:Email/Password` (ou default de dev).
  - **Não** cria empresas/admins no bootstrap (fica para a central).
- `SeedDemoData` (demo, não-prod):
  - Cria uma **empresa demo** (ex.: "Demo Ltda", identificador `demo`, CNPJ/telefones/
    endereço falsos) + Admin demo.
  - Popula clientes/produtos/vendas **sob essa empresa demo**.
  - Idempotente: se já houver produto na empresa demo, não duplica.

### 8.3 Testes
- **Unitários**: `InMemoryDbContextFactory` aceita `Guid? empresaId` e cria
  `ITenantContext` fake. Handlers seguem testados; novos testes do filtro global.
- **Integração**:
  - `DatabaseFixture.ResetDatabaseAsync` passa a limpar também Empresas e usuários
    (ordem correta de FKs).
  - Helper `CriarEmpresaEAdminAsync` para cada classe criar sua empresa no `InitializeAsync`.
- **Novos testes (alto valor — segurança)**:
  - `TenancyIsolationTests`: duas empresas, mesmo telefone/produto → A não vê B;
    handlers de A não alteram B.
  - `LoginFlowTests`: usuário inativo negado; empresa inativa negada; super admin → `/central`;
    admin de empresa → app.
  - `ImpersonationTests`: entrar-como, agir na empresa, voltar à central; badge só p/ super.
  - `EmpresaGerenciamentoTests` (central): criar/editar/inativar empresa, usuários, ver produtos.
  - `ConfiguracaoEmpresaTests`: admin edita nome/cnpj/endereço/telefones/cor/logo; não muda
    identificador; não inativa.
  - `AuditoriaTests`: ações registradas.
- Fluxo **TDD**: testes primeiro → implementar → refatorar.

## 9. Fora de escopo (nesta fase)
- Migração de dados legados (decisão: começar do zero).
- Subdomínio por empresa / multi-banco.
- Convite por e-mail (sem fluxo de invite — admin criado pelo super admin com senha).
- Features futuras citadas (ex.: entrega/geolocalização) que usam o endereço estruturado.
- Recálculo de recorrência/edição de venda intactas (reversões físicas permanecem).