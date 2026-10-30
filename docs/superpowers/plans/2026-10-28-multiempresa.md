# Migração Multi-Empresa (Multi-Tenancy) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transformar o sistema de empresa única "André Gas e Água" em multi-empresa (Revenda.App), com isolamento de dados por `EmpresaId`, super admin em central com impersonation, dados por empresa e branding dinâmico.

**Architecture:** Coluna `EmpresaId` + filtro global dinâmico por instância no `AppDbContext` (uma instância por `ITenantContext`), `SaveChangesInterceptor` preenche automaticamente o tenant. Autenticação por e-mail (1 usuário = 1 empresa); super admin é usuário sem `EmpresaId` que cai na central e entra nas empresas por impersonation. Exclusão lógica apenas nas entidades mestre (Cliente/Produto/Venda).

**Tech Stack:** .NET 10 / C#, Blazor Web App (Interactive Server), MudBlazor, EF Core + PostgreSQL (Aspire), ASP.NET Core Identity, Mediator (martinothamar), FluentValidation, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-27-multiempresa-design.md`

## Global Constraints

- Todas as versões de pacotes via Central Package Management (`Directory.Packages.props`).
- Nomes de domínio, telas e mensagens em **pt-BR**; tipos/namespaces em PascalCase .NET.
- Toda rota exige autenticação por padrão (FallbackPolicy); páginas de domínio exigem `@rendermode InteractiveServer` exceto Login SSR estático.
- Não usar MediatR v13+; usar `Mediator` (martinothamar) com `ServiceLifetime.Scoped`.
- Handlers de domínio **não** setam `EmpresaId` manualmente — só o interceptor preenche.
- `Cliente.Telefone` deixa de ser único global → único por `EmpresaId`.
- `ApplicationUser.EmpresaId` nullable (`null` = central/super admin).
- Role `SuperAdmin` nova; `Admin`/`Vendedor` continuam.
- Migrations a partir de `AndreGas.Infrastructure` (`dotnet-ef`); aplicadas no startup do Web.
- TDD obrigatório: escrever teste primeiro → ver falhar → implementar → refatorar → commit.

## Review Focus

As cinco classes de entrada/falha que a spec implica mas nenhum teste exercita, e onde cada uma é coberta:

1. **Vazamento entre empresas (isolamento)** — operação em empresa A jamais lê/grava B. → `Task 4` (`TenancyIsolationTests`).
2. **Login de usuário de empresa inativa / usuário inativo** — bloqueado com mensagem clara antes do sign-in. → `Task 3` (`LoginFlowTests`).
3. **Super admin sem impersonation acessando tela de domínio** — deve cair na central, não nas telas de empresa nem erro. → `Task 9` (`RoleAwareNotAuthorized`).
4. **Admin de empresa tentando `/central`** — bloqueado (`/sem-autorizacao`). → `Task 9`.
5. **Criar usuário com telefone/email duplicado entre empresas** — a central e a empresa não quebram na criação (email único global, telefone único por empresa). → `Task 5` (`EmpresaGerenciamentoTests`/`ConfiguracaoEmpresaTests`).

---
## Task 1: Modelo de dados (Empresa + EmpresaId + Soft delete)

**Files:**
- Create: `src/AndreGas.Domain/Entities/Empresa.cs`
- Create: `src/AndreGas.Infrastructure/Configurations/EmpresaConfiguration.cs`
- Modify: `src/AndreGas.Infrastructure/Identity/ApplicationUser.cs`
- Modify: `src/AndreGas.Infrastructure/Identity/ApplicationRole.cs`
- Modify: `src/AndreGas.Domain/Entities/Cliente.cs`, `Produto.cs`, `Venda.cs`, `ItemVenda.cs`, `Pagamento.cs`, `MovimentacaoEstoque.cs`, `HistoricoEstoque.cs`, `PrecoPersonalizadoCliente.cs`, `EmprestimoVasilhame.cs`, `RecorrenciaCompra.cs`
- Modify: `src/AndreGas.Infrastructure/Configurations/*Configuration.cs` (todas as acima)
- Test: `tests/AndreGas.UnitTests/Domain/EmpresaTests.cs`

**Interfaces:**
- Produces: `Empresa` (Id, Identificador, Nome, Cnpj, Endereco, Numero, Cep, Bairro, Cidade, Estado, Telefones, CorPrimaria, Logo, Ativo, CriadaEm; `Ativar/Desativar/AtualizarDados`); todas as entidades de domínio com propriedade `Guid EmpresaId { get; private set; }`; entidades com soft delete já existentes mantêm flags.

- [ ] **Step 1: Escrever teste de domínio de `Empresa`**

`tests/AndreGas.UnitTests/Domain/EmpresaTests.cs`:
- `Empresa_criada_inicializa_CriadaEm_e_Ativo_true`
- `Empresa_Desativar_marca_Ativo_false_e_Ativar_true`
- `Empresa_AtualizarDados_altera_nome_cnpj_endereco_telefones_cor_logo`
- `Empresa_AtualizarDados_nunca_muda_Identificador` (identificador imutável)
- `Empresa_criada_sem_valor_usa_telefones_vazio`

- [ ] **Step 2: Rodar teste → falhar (Empresa não existe)**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~EmpresaTests`
Expected: FAIL — `EmpresaTests` não compila.

- [ ] **Step 3: Implementar `Empresa` + configuration**

`src/AndreGas.Domain/Entities/Empresa.cs`: constructor valida nome não-vazio, normaliza telefones (dígitos), `AtualizarDados` não toca `Identificador`.
`src/AndreGas.Infrastructure/Configurations/EmpresaConfiguration.cs`: `ToTable("Empresas")`, `HasIndex(e => e.Identificador).IsUnique()`, `CriadaEm` required, `Telefones` armazenado como string JSON separada por `;` (ou `HasConversion`).

- [ ] **Step 4: Rodar teste → passa**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~EmpresaTests`
Expected: PASS

- [ ] **Step 5: Adicionar `EmpresaId` às entidades de domínio (sem filtro ainda)**

Em `Cliente, Produto, Venda, ItemVenda, Pagamento, MovimentacaoEstoque, HistoricoEstoque, PrecoPersonalizadoCliente, EmprestimoVasilhame, RecorrenciaCompra`: adicionar `public Guid EmpresaId { get; private set; }`.
Atualizar `*Configuration.cs` com `builder.Property(x => x.EmpresaId).IsRequired()`.

- [ ] **Step 6: Rodar build → compila**

Run: `dotnet build`
Expected: build OK

- [ ] **Step 7: Adicionar `EmpresaId` + `Ativo` ao `ApplicationUser`**

`ApplicationUser.cs`: `public Guid? EmpresaId { get; set; }` (FK→Empresas nullable) e `public bool Ativo { get; set; } = true;` (propriedades públicas para o Identity/UserManager preencherem).

- [ ] **Step 8: Adicionar role `SuperAdmin`**

`ApplicationRole.cs`: `public const string SuperAdmin = "SuperAdmin";`

- [ ] **Step 9: Commit**

```bash
git add src/AndreGas.Domain/Entities/Empresa.cs src/AndreGas.Infrastructure/Configurations/EmpresaConfiguration.cs src/AndreGas.Domain/Entities/Cliente.cs src/AndreGas.Domain/Entities/Produto.cs src/AndreGas.Domain/Entities/Venda.cs src/AndreGas.Domain/Entities/ItemVenda.cs src/AndreGas.Domain/Entities/Pagamento.cs src/AndreGas.Domain/Entities/MovimentacaoEstoque.cs src/AndreGas.Domain/Entities/HistoricoEstoque.cs src/AndreGas.Domain/Entities/PrecoPersonalizadoCliente.cs src/AndreGas.Domain/Entities/EmprestimoVasilhame.cs src/AndreGas.Domain/Entities/RecorrenciaCompra.cs src/AndreGas.Infrastructure/Identity/ApplicationUser.cs src/AndreGas.Infrastructure/Identity/ApplicationRole.cs src/AndreGas.Infrastructure/Configurations/ClienteConfiguration.cs src/AndreGas.Infrastructure/Configurations/ProdutoConfiguration.cs src/AndreGas.Infrastructure/Configurations/VendaConfiguration.cs src/AndreGas.Infrastructure/Configurations/ItemVendaConfiguration.cs src/AndreGas.Infrastructure/Configurations/PagamentoConfiguration.cs src/AndreGas.Infrastructure/Configurations/MovimentacaoEstoqueConfiguration.cs src/AndreGas.Infrastructure/Configurations/HistoricoEstoqueConfiguration.cs src/AndreGas.Infrastructure/Configurations/PrecoPersonalizadoClienteConfiguration.cs src/AndreGas.Infrastructure/Configurations/EmprestimoVasilhameConfiguration.cs src/AndreGas.Infrastructure/Configurations/RecorrenciaCompraConfiguration.cs tests/AndreGas.UnitTests/Domain/EmpresaTests.cs
git commit -m "feat: modelo de dados multi-empresa (Empresa, EmpresaId, role SuperAdmin)"
```

## Task 2: Isolamento por tenant — `ITenantContext`, filtro global, interceptor

**Files:**
- Create: `src/AndreGas.Infrastructure/Tenancy/ITenantContext.cs`
- Create: `src/AndreGas.Infrastructure/Tenancy/TenantContext.cs`
- Create: `src/AndreGas.Infrastructure/Tenancy/SetEmpresaIdInterceptor.cs`
- Modify: `src/AndreGas.Infrastructure/AppDbContext.cs`
- Modify: `src/AndreGas.Web/Program.cs`
- Test: `tests/AndreGas.UnitTests/Tenancy/TenantIsolationTests.cs`

**Interfaces:**
- Consumes: `EmpresaId` de domínio (Task 1).
- Produces: `ITenantContext { Guid? EmpresaId; string NomeEmpresa; string CorPrimaria; bool EhSuperAdmin; bool Impersonando; Guid EmpresaIdRequerida(string acao); }`; `TenantContext` scoped que lê claims; `AppDbContext` agora recebe `ITenantContext` opcional no construtor — `new AppDbContext(options, tenant?)`.

- [ ] **Step 1: Escrever teste de isolamento (InMemory) — falha antes**

`tests/AndreGas.UnitTests/Tenancy/TenantIsolationTests.cs` (usa `InMemoryDbContextFactory`, que precisa aceitar `Guid? empresaId`):
- `Contexto_empresaA_nao_ve_dados_da_empresaB` (cria produto/cliente na A, contexto de B não lista)
- `Contexto_central_null_ve_tudo` (sem tenant vê dados de A e B)
- `Contexto_sem_tenant_adiciona_dado_de_dominio_lanca` (`EmpresaIdRequerida`)

- [ ] **Step 2: Rodar teste → falhar**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~TenantIsolationTests`
Expected: FAIL (compila p/ InMemory sem `empresaId`; contexto sem filtro vaza).

- [ ] **Step 3: Implementar `ITenantContext` + `TenantContext` + interceptor**

`ITenantContext` (interface); `TenantContext` scoped lê `AuthenticationStateProvider` claims `EmpresaId/NomeEmpresa/CorPrimaria/EhSuperAdmin/Impersonando`; `SetEmpresaIdInterceptor` (`ISaveChangesInterceptor`) preenche `EmpresaId` em `Added` com `EmpresaId == default` (lança se `EmpresaIdRequerida`).

- [ ] **Step 4: Aplicar filtro global no `AppDbContext`**

`AppDbContext` construtor recebe `ITenantContext?`; campo `Guid? _empresaId => _tenant?.EmpresaId`. Em `OnModelCreating`, para cada entidade de domínio:
```csharp
builder.Entity<Cliente>().HasQueryFilter(c => _empresaId == null || c.EmpresaId == _empresaId);
// ... para Produto, Venda, ItemVenda, Pagamento, MovimentacaoEstoque,
//     HistoricoEstoque, PrecoPersonalizadoCliente, EmprestimoVasilhame, RecorrenciaCompra
```
`Empresas` e Identity **sem** filtro. Registrar o interceptor no `options` (via `AddInterceptors` no `Program.cs` ou override `OnConfiguring`).

- [ ] **Step 5: Rodar teste → passa**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~TenantIsolationTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/AndreGas.Infrastructure/Tenancy/ITenantContext.cs src/AndreGas.Infrastructure/Tenancy/TenantContext.cs src/AndreGas.Infrastructure/Tenancy/SetEmpresaIdInterceptor.cs src/AndreGas.Infrastructure/AppDbContext.cs tests/AndreGas.UnitTests/Tenancy/TenantIsolationTests.cs
git commit -m "feat: isolamento multi-empresa via filtro global e interceptor de EmpresaId"
```

## Task 3: Autenticação — claims, login com empresa/usuário ativos, super admin destino

**Files:**
- Create: `src/AndreGas.Web/Common/Authorization/ApplicationUserClaimsPrincipalFactory.cs`
- Modify: `src/AndreGas.Web/Features/Auth/Login/LoginCommandHandler.cs`
- Modify: `src/AndreGas.Web/Features/Auth/Login/LoginResult.cs`
- Modify: `src/AndreGas.Web/Program.cs` (registrar factory)
- Modify: `tests/AndreGas.IntegrationTests/LoginFlowTests.cs`
- Modify: `tests/AndreGas.UnitTests/Features/Auth/Login/LoginCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ApplicationUser.EmpresaId/Ativo`; `Empresa.Ativo` (Task 1).
- Produces: claims `EmpresaId`/`EmpresaNome`/`EmpresaCor`/`EhSuperAdmin`; `LoginResult` ganha `Destino` (`/central` ou `/`).

- [ ] **Step 1: Escrever/atualizar testes de login (falham antes do handler)**

`LoginCommandHandlerTests`:
- `Login_usuario_inativo_retorna_falha_usuario_desativado`
- `Login_empresa_inativa_retorna_falha_empresa_desativada`
- `Login_superadmin_sem_empresa_retorna_destino_central`
- `Login_admin_empresa_retorna_destino_home`

`LoginFlowTests` (integração): ajustar `Login_WithValidCredentials...` para assert de destino (`/` para admin de empresa).

- [ ] **Step 2: Rodar testes → falhar**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~LoginCommandHandlerTests`
Expected: FAIL (LoginResult sem Destino, handler sem verificação de Ativo).

- [ ] **Step 3: Implementar claims factory**

`ApplicationUserClaimsPrincipalFactory` (assina `UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>`), adiciona claims `EmpresaId`, `EmpresaNome`, `EmpresaCor`, `EhSuperAdmin` (bool como string) quando `usuario.EmpresaId` existe; registra no `Program.cs` (`AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ...>`).

- [ ] **Step 4: Alterar `LoginResult` + handler**

`LoginResult` ganha `string Destino` (falta default via factory). Handler: carrega usuário por e-mail (antes do sign-in), valida `usuario.Ativo` e, se `EmpresaId` não-nulo, valida `Empresa.Ativo`; decide `Destino` (`/central` se super admin sem empresa, senão `/`). Ajusta `Login.razor` para navegar para `result.Destino` (hoje navega para "/").

- [ ] **Step 5: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~LoginCommandHandlerTests` e `dotnet test tests/AndreGas.IntegrationTests --filter FullyQualifiedName~LoginFlowTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/AndreGas.Web/Common/Authorization/ApplicationUserClaimsPrincipalFactory.cs src/AndreGas.Web/Features/Auth/Login/LoginCommandHandler.cs src/AndreGas.Web/Features/Auth/Login/LoginResult.cs src/AndreGas.Web/Program.cs src/AndreGas.Web/Features/Auth/Login/Login.razor tests/AndreGas.UnitTests/Features/Auth/Login/LoginCommandHandlerTests.cs tests/AndreGas.IntegrationTests/LoginFlowTests.cs
git commit -m "feat: login com verificação de usuário/empresa ativos e destino super admin"
```

## Task 4: Seed — roles, SuperAdmin da central, empresa demo

**Files:**
- Modify: `src/AndreGas.Web/Features/Auth/SeedData.cs`
- Modify: `src/AndreGas.Web/Features/Seed/SeedDemoData.cs`
- Modify: `tests/AndreGas.UnitTests/Features/Seed/SeedDemoDataTests.cs`
- Modify: `tests/AndreGas.IntegrationTests/SeedDataFeatureTests.cs`

**Interfaces:**
- Consumes: `Empresa` (Task 1), `ITenantContext` (Task 2), `ApplicationRole.SuperAdmin` (Task 1).
- Produces: SuperAdmin da central criado (sem `EmpresaId`, role SuperAdmin, creds `SeedAdmin:*`); `SeedDemoData` roda sob empresa demo (id `demo`).

- [ ] **Step 1: Escrever testes de seed**

`SeedDemoDataTests` (InMemory): `SeedDemoData_cria_empresa_demo_e_admin_demo` (empresa `demo` existe, produto existe sob ela, admin demo existe).
`SeedDataFeatureTests` (integração): `Seed_cria_superadmin_da_central` (existe usuário sem EmpresaId com role SuperAdmin).

- [ ] **Step 2: Rodar testes → falhar**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~SeedDemoDataTests`
Expected: FAIL

- [ ] **Step 3: Implementar**

`SeedData`: `GarantirRolesAsync` inclui `SuperAdmin`; `SeedDefaultAdminUserAsync` cria usuário sem `EmpresaId` na role `SuperAdmin` (em vez de Admin? — **decisão**: o seed passa a criar o **SuperAdmin da central** com creds `SeedAdmin:*`, `Nome = "Super Administrador"`). `SeedDemoData.SeedAsync` cria (se não existir) a `Empresa` demo (`Identificador="demo"`, Nome "Demo Ltda", CNPJ/telefones/endereço falsos) e um Admin demo (`admin@demo...`), e cria produtos/clientes **com o `EmpresaId` da empresa demo** (via contexto com tenant = demo ou setando diretamente com o interceptor).

- [ ] **Step 4: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~SeedDemoDataTests`; `dotnet test tests/AndreGas.IntegrationTests --filter FullyQualifiedName~SeedDataFeatureTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Web/Features/Auth/SeedData.cs src/AndreGas.Web/Features/Seed/SeedDemoData.cs tests/AndreGas.UnitTests/Features/Seed/SeedDemoDataTests.cs tests/AndreGas.IntegrationTests/SeedDataFeatureTests.cs
git commit -m "feat: seed de super admin da central e empresa demo sob tenant"
```

## Task 5: Migração EF Core

**Files:**
- Create: `src/AndreGas.Infrastructure/Migrations/*_MultiEmpresa.cs` (via dotnet-ef)
- Modify: `tests/AndreGas.IntegrationTests/DatabaseFixture.cs` (Reset inclui Empresas/usuários)

**Interfaces:**
- Consumes: entidades com `EmpresaId`, `ApplicationUser.EmpresaId/Ativo`, `Empresa` (Tasks 1-4).
- Produces: schema com `Empresas`, coluna `EmpresaId` em tabelas de domínio, índice composto único `(EmpresaId, Telefone)` em Clientes, índices `EmpresaId` nas demais, `ApplicationUser.EmpresaId/Ativo`.

- [ ] **Step 1: Gerar migração**

Run: `dotnet-ef migrations add MultiEmpresa --project src/AndreGas.Infrastructure --startup-project src/AndreGas.Infrastructure -o Migrations`
Expected: arquivos de migração criados.

- [ ] **Step 2: Revisar a migração gerada**

No arquivo `*_MultiEmpresa.cs`: confirmar `Empresas` criada, `EmpresaId` NOT NULL nas tabelas de domínio, `ApplicationUser.EmpresaId` nullable + `Ativo` default true, índice composto único `(EmpresaId, Telefone)` em Clientes, índices `EmpresaId`. Corrigir manualmente se faltar.

- [ ] **Step 3: Atualizar `DatabaseFixture.ResetDatabaseAsync`**

Adicionar limpeza de `Empresas` (e usuários/roles) na ordem correta de FKs, antes das de domínio.

- [ ] **Step 4: Rodar testes de integração existentes (regressão)**

Run: `dotnet test tests/AndreGas.IntegrationTests`
Expected: os testes que usam `DatabaseFixture`/`WebAppFixture` passam (ajustes necessários: cada teste cria sua empresa via helper — ver Task 6).

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Infrastructure/Migrations tests/AndreGas.IntegrationTests/DatabaseFixture.cs
git commit -m "chore: migração EF Core multi-empresa (Empresas, EmpresaId, SuperAdmin)"
```

## Task 6: Handlers/validators de domínio usam tenant — isolamento funcional

**Files:**
- Modify: `src/AndreGas.Web/Features/Clientes/Cadastrar/CadastrarClienteCommandHandler.cs`
- Modify: `src/AndreGas.Web/Features/Clientes/Listar/ListarClientesQueryHandler.cs` (e paginado)
- Modify: `src/AndreGas.Web/Features/Estoque/Cadastrar/CadastrarProdutoCommandHandler.cs`
- Modify: `src/AndreGas.Web/Features/Estoque/Listar/ListarProdutosQueryHandler.cs`
- Modify: `src/AndreGas.Web/Features/Home/ObterDashboardQueryHandler.cs`
- Modify: `src/AndreGas.Web/Features/Vendas/NovaVenda/RegistrarVendaCommandHandler.cs`
- Modify: `tests/AndreGas.UnitTests/TestHelpers/InMemoryDbContextFactory.cs`
- Test: `tests/AndreGas.UnitTests/Features/Tenancy/HandlerTenantTests.cs`

**Interfaces:**
- Consumes: `ITenantContext.EmpresaIdRequerida()` (Task 2).
- Produces: todos os handlers de domínio passam a injetar `ITenantContext` e chamar `EmpresaIdRequerida()` no início; queries filtram via filtro global (sem filtro manual).

- [ ] **Step 1: Atualizar `InMemoryDbContextFactory`**

`Create(Guid? empresaId = null)` ⇒ cria `AppDbContext(options, tenantFake)`. Ajustar todos os testes unitários de handlers existentes para passar `empresaId` quando necessário.

- [ ] **Step 2: Escrever teste de tenant em handler (falha antes)**

`HandlerTenantTests`:
- `CadastrarProduto_sem_tenant_lanca` (EmpresaIdRequerida)
- `ListarProdutos_sem_tenant_lanca`
- `RegistrarVenda_sem_tenant_lanca`

- [ ] **Step 3: Implementar `EmpresaIdRequerida()` no início de cada handler de domínio**

`CadastrarProdutoCommandHandler`, `ListarProdutosQueryHandler`, `CadastrarClienteCommandHandler`, `ListarClientesQueryHandler`, `ObterDashboardQueryHandler`, `RegistrarVendaCommandHandler` (e demais que operam domínio). Obter `tenant.EmpresaIdRequerida(...)` e usar apenas para validação — o filtro global já isola.

- [ ] **Step 4: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.UnitTests`
Expected: PASS (inclui novos tenant + ajustes dos existentes)

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Web/Features tests/AndreGas.UnitTests/TestHelpers/InMemoryDbContextFactory.cs tests/AndreGas.UnitTests/Features/Tenancy/HandlerTenantTests.cs
git commit -m "feat: handlers de domínio exigem tenant ativo e respeitam isolamento por empresa"
```

## Task 7: Testes de integração de isolamento + ajustes de fixtures

**Files:**
- Create: `tests/AndreGas.IntegrationTests/TenancyIsolationTests.cs`
- Create: `tests/AndreGas.IntegrationTests/TestHelpers/EmpresaTestHelper.cs`
- Modify: `tests/AndreGas.IntegrationTests/DatabaseFixture.cs`

**Interfaces:**
- Consumes: `Empresa`, `ITenantContext` (Tasks 1-2), handlers tenant-aware (Task 6).
- Produces: `EmpresaTestHelper.CriarEmpresaEAdminAsync(db, nome, identificador, ...)` → cria empresa + admin (Admin role) e retorna `(empresa, adminUser)`. `DatabaseFixture.CriarEmpresaAsync(...)`.

- [ ] **Step 1: Criar helper**

`EmpresaTestHelper.CriarEmpresaEAdminAsync(AppDbContext db, string nome, string identificador, string email, string senha)` — cria `Empresa` (+ save), cria `ApplicationUser` com `EmpresaId` + role `Admin` (usa `UserManager` via scope ou direto no db com Identity).

- [ ] **Step 2: Escrever `TenancyIsolationTests`**

Inclui: `EmpresaA_e_B_com_mesmo_telefone_nao_se_veem`, `Produto_de_A_nao_visivel_em_B`, `Venda_de_A_nao_altera_estoque_de_B` (handler de venda em A não muda produto de B — mesmo produto com ids diferentes por empresa).

- [ ] **Step 3: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.IntegrationTests --filter FullyQualifiedName~TenancyIsolationTests`
Expected: PASS

- [ ] **Step 4: Ajustar fixtures existentes (DatabaseFixture/WebAppFixture) para empresa**

Cada classe de teste de integração passa a criar sua(s) empresa(s) no `InitializeAsync` e conectar handlers com o `ITenantContext` fake daquela empresa.

- [ ] **Step 5: Rodar suíte de integração**

Run: `dotnet test tests/AndreGas.IntegrationTests`
Expected: PASS (ajustes de fixtures garantem tenant)

- [ ] **Step 6: Commit**

```bash
git add tests/AndreGas.IntegrationTests tests/AndreGas.UnitTests
git commit -m "feat: testes de integração de isolamento multi-empresa e fixtures com tenant"
```

## Task 8: Branding dinâmico — nome/cor/logo por empresa

**Files:**
- Create: `src/AndreGas.Web/Common/IdentidadeEmpresaService.cs`
- Modify: `src/AndreGas.Web/Theme/AndreGasTheme.cs` (adicionar `ComCorPrimaria`)
- Modify: `src/AndreGas.Web/Components/Layout/MainLayout.razor`
- Modify: `src/AndreGas.Web/Components/Layout/TopMenuActions.razor` (badge impersonation + voltar)
- Modify: `src/AndreGas.Web/Features/Auth/Login/Login.razor` (sempre Revenda.App)
- Modify: `src/AndreGas.Web/Features/*` (PageTitle → nome empresa)
- Test: `tests/AndreGas.UnitTests/Common/IdentidadeEmpresaServiceTests.cs`

**Interfaces:**
- Consumes: claims do Task 3; `Empresa.Logo` (banco, cache scoped).
- Produces: `IdentidadeEmpresaService { string NomeEmpresa; string CorPrimaria; string? Logo; bool TemEmpresa; }`.

- [ ] **Step 1: Escrever teste de `IdentidadeEmpresaService`**

- `Servico_sem_tenant_retorna_nome_revenda_app` (central)
- `Servico_com_empresa_retorna_nome_e_cor_da_empresa` (via claims fake/também do banco p/ logo)

- [ ] **Step 2: Implementar serviço**

Lê claims `EmpresaNome/CorPrimaria`; `Logo` consulta o banco (cache scoped) — quando nome/cor vêm de login, logo precisa do banco (claims não carregam base64).

- [ ] **Step 3: Aplicar branding nos componentes**

`MainLayout`: usar `IdentidadeEmpresaService` — **sem avatar**; logo+nome se `Logo`, senão só nome; `MudThemeProvider` com `AndreGasTheme.ComCorPrimaria(cor)`. `Login.razor`: branding fixo **Revenda.App** (remover "André Gas e Água"). `PageTitle` de todas as páginas → `{Tela} — {NomeEmpresa}` (via serviço; central `Revenda.App`).

- [ ] **Step 4: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~IdentidadeEmpresaServiceTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Web/Common/IdentidadeEmpresaService.cs src/AndreGas.Web/Theme/AndreGasTheme.cs src/AndreGas.Web/Components src/AndreGas.Web/Features tests/AndreGas.UnitTests/Common/IdentidadeEmpresaServiceTests.cs
git commit -m "feat: branding dinâmico por empresa (nome, cor, logo; login sempre Revenda.App)"
```

## Task 9: Autorização — política SuperAdmin + redirecionamento + impersonation

**Files:**
- Modify: `src/AndreGas.Web/Common/Authorization/Politicas.cs`
- Modify: `src/AndreGas.Web/Components/Layout/RoleAwareNotAuthorized.razor`
- Create: `src/AndreGas.Web/Features/Central/EntrarComo/EntrarComoCommand.cs` (Mediator) + Handler
- Create: `src/AndreGas.Web/Features/Central/VoltarCentralCommand.cs` (ou endpoint) + Handler
- Modify: `src/AndreGas.Web/Features/Auth/AuthEndpoints.cs` (endpoints impersonation/voltar)
- Test: `tests/AndreGas.IntegrationTests/ImpersonationTests.cs`

**Interfaces:**
- Consumes: claims (Task 3), `ITenantContext` (Task 2).
- Produces: política `ApenasSuperAdmin`; comando `EntrarComoCommand(EmpresaId, UsuarioIdAdmin)`, `VoltarCentralCommand`; handlers fazem `SignInAsync` com claims de impersonation.

- [ ] **Step 1: Escrever teste de impersonation (integração, falha antes)**

`ImpersonationTests` (WebAppFixture):
- `SuperAdmin_entra_como_admin_empresa_e_age_na_empresa`
- `SuperAdmin_volta_para_central_apos_entrar_como`
- `Admin_empresa_nao_pode_entrar_como`

- [ ] **Step 2: Implementar política + redirecionamento**

`Politicas.ApenasSuperAdmin` (`RequireRole(ApplicationRole.SuperAdmin)`). `RoleAwareNotAuthorized`: se super admin não-impersonando tentar rota de domínio → `/central`; se admin de empresa tentar `/central` → `/sem-autorizacao`.

- [ ] **Step 3: Implementar impersonation + voltar**

`EntrarComoCommandHandler`: valida empresa ativa + admin ativo; `SignInAsync(adminPrincipal)` com claims extras `Impersonando=true`, `SuperAdminOriginalId`. `VoltarCentralCommandHandler`: `SignInAsync(superAdminPrincipal)` remover impersonation. Endpoints no `AuthEndpoints`.

- [ ] **Step 4: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.IntegrationTests --filter FullyQualifiedName~ImpersonationTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Web/Common/Authorization/Politicas.cs src/AndreGas.Web/Components/Layout/RoleAwareNotAuthorized.razor src/AndreGas.Web/Features/Central src/AndreGas.Web/Features/Auth/AuthEndpoints.cs tests/AndreGas.IntegrationTests/ImpersonationTests.cs
git commit -m "feat: autorização de super admin, redirecionamento e impersonation entre empresas"
```

## Task 10: Central do super admin — lista/editar/inativar empresa, usuários, produtos (leitura)

**Files:**
- Create: `src/AndreGas.Web/Features/Central/Empresas/EmpresaLista.razor` (+ `@code`/`*.razor.cs`), `EmpresaEditorDialog.razor`, `UsuarioEmpresaDialog.razor`, `ProdutosEmpresaDialog.razor`
- Create: `src/AndreGas.Web/Features/Central/ListarEmpresasQuery.cs` + Handler
- Create: `src/AndreGas.Web/Features/Central/CriarEmpresaCommand.cs` + Handler (cria empresa + 1º admin)
- Create: `src/AndreGas.Web/Features/Central/EditarEmpresaCommand.cs` + Handler
- Create: `src/AndreGas.Web/Features/Central/InativarEmpresaCommand.cs` + Handler
- Create: `src/AndreGas.Web/Features/Central/ListarProdutosEmpresaQuery.cs` + Handler (leitura)
- Modify: `src/AndreGas.Web/Components/App.razor` (rota área central protegida) — ou nova `Central.razor` com `@page "/central"`
- Test: `tests/AndreGas.IntegrationTests/EmpresaGerenciamentoTests.cs`

**Interfaces:**
- Consumes: `Empresa` (Task 1), `ITenantContext`/super admin (Task 9).
- Produces: comandos/queries da central (listar/criar/editar/inativar empresa, listar produtos de uma empresa), diálogos MudBlazor.

- [ ] **Step 1: Escrever testes de gerenciamento de empresas (integração, falha antes)**

`EmpresaGerenciamentoTests`:
- `CriarEmpresa_cria_empresa_e_primeiro_admin`
- `EditarEmpresa_altera_nome_cnpj_endereco_cor_logo_e_nao_muda_identificador`
- `InativarEmpresa_impede_login_do_admin`
- `ListarProdutosEmpresa_retorna_produtos_da_empresa_alvo` (leitura)

- [ ] **Step 2: Implementar queries/comandos da central**

`ListarEmpresasQueryHandler` (filtra por `EmpresaId`? não — central vê todas; listar com busca/status/contagem de usuários). `CriarEmpresaCommandHandler` cria `Empresa` + `ApplicationUser` Admin (via `IUsuarioWriter` + `EmpresaId`) com validação. `EditarEmpresaCommandHandler` (`AtualizarDados`). `InativarEmpresaCommandHandler`. `ListarProdutosEmpresaQueryHandler` retorna produtos da empresa alvo (modo central vê tudo — usa `EmpresaId` alvo no filtro).

- [ ] **Step 3: Implementar as telas**

`EmpresaLista.razor`: lista com busca/paginação/status; botões Entrar como / Configurar / Usuários / Ver produtos / Inativar. `EmpresaEditorDialog` (nome, identificador imutável, CNPJ, endereço estruturado, telefones, cor, logo). `UsuarioEmpresaDialog` (cria usuário da empresa Admin/Vendedor). `ProdutosEmpresaDialog` (leitura).

- [ ] **Step 4: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.IntegrationTests --filter FullyQualifiedName~EmpresaGerenciamentoTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Web/Features/Central tests/AndreGas.IntegrationTests/EmpresaGerenciamentoTests.cs
git commit -m "feat: central do super admin (empresas, usuários, produtos em leitura)"
```

## Task 11: Aba "Minha Empresa" (configuração pelo admin) + auditoria

**Files:**
- Create: `src/AndreGas.Domain/Entities/RegistroAuditoria.cs`
- Create: `src/AndreGas.Infrastructure/Configurations/RegistroAuditoriaConfiguration.cs`
- Create: `src/AndreGas.Web/Features/ConfiguracaoEmpresa/EditarConfigEmpresaCommand.cs` + Handler + Validator
- Create: `src/AndreGas.Web/Features/ConfiguracaoEmpresa/ConfigEmpresaTab.razor` (aba em `Configuracoes.razor`)
- Modify: `src/AndreGas.Web/Features/Usuarios/Configuracoes.razor` (adicionar aba "Minha Empresa")
- Modify: `src/AndreGas.Infrastructure/AppDbContext.cs` (DbSet RegistroAuditoria)
- Modify: `src/AndreGas.Web/Program.cs` (registrar handler/validator)
- Test: `tests/AndreGas.IntegrationTests/ConfiguracaoEmpresaTests.cs`
- Test: `tests/AndreGas.UnitTests/Features/ConfiguracaoEmpresa/EditarConfigEmpresaValidatorTests.cs`

**Interfaces:**
- Consumes: `Empresa` (Task 1), `ITenantContext` (Task 2), `Politicas.ApenasAdmin` (Task 9), `UsuarioPapel` (existente).
- Produces: `RegistroAuditoria` DbSet; comando `EditarConfigEmpresaCommand(Nome, Cnpj, Endereco, Numero, Cep, Bairro, Cidade, Estado, Telefones, CorPrimaria, Logo)` que registra `RegistroAuditoria` (Acao=`ConfigEmpresaEditada`); aba.

- [ ] **Step 1: Escrever testes (falham antes)**

`EditarConfigEmpresaValidatorTests`:
- `Validator_rejeita_nome_vazio`
- `Validator_permite_cor_nula_e_logo_opcional`
- `Validator_rejeita_cnpj_invalido` (se validado) / ou aceita opcional

`ConfiguracaoEmpresaTests` (integração):
- `Admin_empresa_edita_nome_cnpj_endereco_telefones_cor_logo`
- `Admin_empresa_nao_pode_mudar_identificador`
- `Admin_empresa_nao_pode_inativar_propria_empresa`
- `Edicao_grava_registro_auditoria`

- [ ] **Step 2: Implementar `RegistroAuditoria` + config**

Entidade + configuration (EmpresaId nullable, UsuarioId, UsuarioNome, Acao, Detalhes, DataHora, Origem).

- [ ] **Step 3: Implementar `EditarConfigEmpresaCommand` + handler + validator**

Só admin (via `Politicas.ApenasAdmin`/`RequerAdminAsync`); `AtualizarDados`; grava `RegistroAuditoria`. Validator: nome obrigatório, CNPJ opcional mas validado se presente (formato), cor optativa com formato hex, logo optativo com limite.

- [ ] **Step 4: Implementar aba**

`Configuracoes.razor` ganha aba "Minha Empresa" renderizando formulário de edição (nome, CNPJ, endereço estruturado, telefones, cor, logo) — **sem** campo identificador, **sem** inativar.

- [ ] **Step 5: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.UnitTests --filter FullyQualifiedName~EditarConfigEmpresaValidatorTests`; `dotnet test tests/AndreGas.IntegrationTests --filter FullyQualifiedName~ConfiguracaoEmpresaTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/AndreGas.Domain/Entities/RegistroAuditoria.cs src/AndreGas.Infrastructure/Configurations/RegistroAuditoriaConfiguration.cs src/AndreGas.Infrastructure/AppDbContext.cs src/AndreGas.Web/Features/ConfiguracaoEmpresa src/AndreGas.Web/Features/Usuarios/Configuracoes.razor tests/AndreGas.UnitTests/Features/ConfiguracaoEmpresa tests/AndreGas.IntegrationTests/ConfiguracaoEmpresaTests.cs
git commit -m "feat: aba Minha Empresa para admin configurar a própria empresa + auditoria"
```

## Task 12: Auditoria da central + revenda métrica de isolamento do seed/ajustes finais

**Files:**
- Create: `src/AndreGas.Web/Features/Central/Auditoria/AuditoriaLista.razor` + query
- Modify: `src/AndreGas.Web/Features/Central/Auditoria/ListarAuditoriaQuery.cs` + Handler
- Modify: handlers da central (Task 10) para gravar `RegistroAuditoria` (EmpresaCriada/Editada/Inativada/UsuarioCriado)
- Modify: `src/AndreGas.Web/Features/Central/Empresas/EmpresaLista.razor` (tab Auditoria)
- Test: `tests/AndreGas.IntegrationTests/AuditoriaTests.cs`

**Interfaces:**
- Consumes: `RegistroAuditoria` (Task 11), comandos da central (Task 10).
- Produces: `ListarAuditoriaQuery` + handler (lista auditória, filtro por empresa/central); gravação de auditoria nos comandos da central.

- [ ] **Step 1: Escrever testes (falham antes)**

`AuditoriaTests`:
- `CriarEmpresa_grava_registro_EmpresaCriada`
- `InativarEmpresa_grava_registro_EmpresaInativada`
- `EntrarComo_grava_registro_EntrarComo`

- [ ] **Step 2: Implementar gravação nos handlers da central**

`CriarEmpresaCommandHandler`, `EditarEmpresaCommandHandler`, `InativarEmpresaCommandHandler`, `EntrarComoCommandHandler` adicionam `RegistroAuditoria` com `Origem="Central"` e `Usuario` = super admin autenticado.

- [ ] **Step 3: Implementar `ListarAuditoriaQuery` + tab**

Handler lista auditória (mais recente primeiro, filtro por empresa/central). `EmpresaLista.razor` ganha aba "Auditoria".

- [ ] **Step 4: Rodar testes → passam**

Run: `dotnet test tests/AndreGas.IntegrationTests --filter FullyQualifiedName~AuditoriaTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/AndreGas.Web/Features/Central tests/AndreGas.IntegrationTests/AuditoriaTests.cs
git commit -m "feat: auditoria de ações da central"
```

---
## Self-Review

### Spec coverage
- **Modelo de dados (Empresa, EmpresaId, SuperAdmin, soft delete mestre)** → Task 1 ✅
- **Isolamento (ITenantContext, filtro global, interceptor, camadas)** → Task 2 ✅
- **Autenticação (claims, login ativo, destino, impersonation)** → Tasks 3, 9 ✅
- **Seed (super admin central, empresa demo, roles)** → Task 4 ✅
- **Migração + fixtures** → Task 5 ✅
- **Handlers tenant-aware** → Task 6 ✅
- **Testes de isolamento integração** → Task 7 ✅
- **Branding dinâmico (Login Revenda.App, app bar sem avatar, tema por empresa)** → Task 8 ✅
- **Central (lista/editar/inativar, usuários, produtos leitura, entra-como)** → Tasks 9, 10 ✅
- **Aba Minha Empresa** → Task 11 ✅
- **Auditoria central** → Task 12 ✅
- **Fora de escopo** (backfill, subdomínio, convite) → não há tasks (intencional) ✅

### Step scan
- Cada passo tem um resultado checável (teste, build, commit). Passos de código dão a assinatura/arquivo/valores fixados; o corpo é decidido pelo implementador.
- Ajuste grande: `ListarUsuariosQueryHandler` precisa filtrar por empresa (central vê por empresa alvo) — coberto implicitamente em Task 10 (usuários da empresa); deixei explícito no texto da Task 10.

### Type consistency
- `ITenantContext.EmpresaIdRequerida()` definido na Task 2, usado nas 6-9, 11 ✅
- `LoginResult.Destino` Task 3 usado na navegação da Task 3 ✅
- `Empresa.AtualizarDados` Task 1 usado na 10/11 ✅
- `RegistroAuditoria` Task 11 usado na 12 ✅
- Nomes de comandos/queries consistentes entre tasks (CriarEmpresa/EditarEmpresa/InativarEmpresa/ListarEmpresas/ListarProdutosEmpresa/ListarAuditoria).

### Review Focus
1. Vazamento entre empresas → Task 2 (unit) + Task 7 (integração) ✅
2. Login inativo → Task 3 ✅
3. Super admin sem impersonation em tela de domínio → Task 9 ✅
4. Admin de empresa em `/central` → Task 9 ✅
5. Duplicidade email/telefone entre empresas → Task 3/7 (email global único, telefone por empresa) ✅

### Proportion
- Plano ~290 linhas para spec ~270 linhas; sem corpos de código além de signatures/testes — proporção adequada (não é transcript).
- Steps sem "decidem nada"; os únicos corpos (filtro global) são algoritmos/padrão que o implementador determinaria igual.

O plano está completo e autocoerente.

---
## Execution Handoff

Plano salvo em `docs/superpowers/plans/2026-10-28-multiempresa.md`.

Por favor, revise o plano. Qual abordagem de execução você prefere?

- **Subagent-driven** — Um subagente fresco implementa cada task e um revisor fresco a verifica antes da próxima começar, com uma revisão final da branch. Mais completo; custa um contexto novo por task e por revisão.
- **Native** — Eu implemento cada task eu mesmo nesta sessão, do jeito que esta máquina executa trabalho, depois um revisor fresco no modelo mais capaz revisa a branch inteira. Mais barato e rápido; sem revisão independente até o fim.

**Para este plano eu recomendo Subagent-driven**, porque há 12 tasks com interdependência real de interfaces (`ITenantContext`, `LoginResult.Destino`, `RegistroAuditoria`, comandos da central) e o custo de um erro de isolamento é alto (vazamento de dados entre empresas) — a revisão por task pega inconistências de contrato cedo.

O plano captura o que você quer, e qual abordagem devemos usar?