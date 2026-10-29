# Revenda.App

Sistema de gestão para revendas, **multi-empresa (multi-tenant)**: clientes (com controle de
fiado/saldo devedor), estoque, vendas e dashboards gerenciais. O primeiro cliente é uma
revendedora de gás de cozinha e água mineral — o domínio de vendas, fiado e vasilhames foi
modelado a partir dela.

Cada empresa (tenant) tem nome, cor e logo próprios e enxerga **apenas** os seus dados. A
marca do produto (Revenda.App) aparece no login e no modo central, onde o super administrador
gerencia as empresas.

## Stack
- **.NET 10** / C#
- **.NET Aspire** — orquestração local (Postgres, dashboard de telemetria)
- **Blazor Web App** (Interactive Server) + **MudBlazor** — front-end
- **EF Core** + **PostgreSQL** — persistência
- **Multi-tenancy**: filtro global por `EmpresaId` em cada entidade de domínio no `AppDbContext`
  + `SetEmpresaIdInterceptor` (interceptor de `SaveChanges`) carimbando o `EmpresaId` das linhas
  novas. O `AppDbContext` é **scoped**, não pooled — ver a seção de multi-empresa no
  [AGENTS.md](AGENTS.md).
- **ASP.NET Core Identity** — autenticação (papéis `SuperAdmin`, `Admin` e `Vendedor`)
- **Mediator** (martinothamar) + **FluentValidation** — casos de uso (vertical slices)
- **xUnit** — testes unitários e de integração (TDD)

## Estrutura
```
src/
  AndreGas.AppHost/          Orquestração Aspire (Postgres, projeto Web)
  AndreGas.ServiceDefaults/  Telemetria, health checks, resiliência
  AndreGas.Domain/           Entidades e enums de domínio
  AndreGas.Infrastructure/   EF Core (AppDbContext), Identity, migrations
  AndreGas.Web/              Blazor Web App + MudBlazor (Features/ = vertical slices)
tests/
  AndreGas.UnitTests/        Testes unitários (handlers, validators, cálculos)
  AndreGas.IntegrationTests/ Testes de integração (Aspire.Hosting.Testing + Postgres real)
```

## Como rodar
Pré-requisitos: .NET 10 SDK (ver [global.json](global.json)) e Docker (para o container do
Postgres via Aspire).

```bash
dotnet run --project src/AndreGas.AppHost
```

Isso abre o **Aspire Dashboard** com os logs/telemetria de todos os recursos, incluindo o
container do Postgres e a aplicação web.

### Bootstrap: super admin da central × empresa demo
Na inicialização o `AndreGas.Web` aplica as migrations e roda dois seeds independentes:

- **Super admin da central** (`Features/Auth/SeedData.cs`): cria **um** usuário na role
  `SuperAdmin`, **sem empresa** (`EmpresaId = null`). É ele que acessa `/central`. As credenciais
  vêm de `SeedAdmin:Email` / `SeedAdmin:Password` (o AppHost repassa os parâmetros
  `seed-admin-email` / `seed-admin-password`); sem essas chaves, o seed usa os defaults de
  desenvolvimento `admin@andregas.com.br` / `AndreGas@123`. **Em Produção não há fallback**: se
  as duas chaves não estiverem configuradas, o seed lança e a aplicação não sobe.
  Este seed **não** cria empresa nem admin de empresa.
- **Empresa demo** (`Features/Seed/SeedDemoData.cs`): cria a empresa `Demo Ltda` e o admin dela
  (`admin@demo.andregas.com.br` / `Demo@123`, role `Admin`), mais clientes, produtos, vendas,
  pagamentos e movimentações de estoque de demonstração. É idempotente e roda **somente fora de
  Produção**, controlado por `Seed:EnableDemoData` (default: `!IsProduction()`; em
  `appsettings.Production.json` está `false`).

### Importante para volumes antigos do Postgres
O Postgres do AppHost usa volume persistente (`.WithDataVolume()`), e o schema mudou para
multi-empresa na migração `MultiEmpresa`. Um volume criado **antes** dessa mudança tem dados
legados cujo `EmpresaId` foi preenchido com `Guid.Empty` — essas linhas não pertencem a nenhuma
empresa (aparecem só no modo central, nunca na empresa). Para um ambiente limpo, recrie o volume:

```bash
docker volume ls | grep andregas            # descubra o <hash>
docker volume rm andregas.apphost-<hash>-postgres-data
```

A senha do usuário `postgres` é **fixa** no AppHost (`postgres-password`, valor
`andregas-dev-only`) justamente por causa desse volume — **não remova** essa senha fixa, senão o
Aspire gera uma senha nova a cada execução e o volume antigo passa a recusar a autenticação.

## Testes
```bash
dotnet test
```

A suíte de integração (`tests/AndreGas.IntegrationTests`) sobe um **Postgres real via Aspire**
(nada de mocks de banco) e roda **serializada**: o `AssemblyInfo.cs` declara
`[assembly: CollectionBehavior(DisableTestParallelization = true)]`, porque cada classe sobe seu
próprio container e rodar em paralelo causa falhas transitórias de conexão. Rodar só os
unitários (rápido, sem Docker): `dotnet test tests/AndreGas.UnitTests`.

## Primeiros passos na aplicação
1. Entre com o **super admin da central** (credenciais acima) — o login dele redireciona para
   `/central`.
2. Em `/central`, clique em **Nova empresa** e informe os dados da empresa **e do primeiro
   administrador** dela (nome, e-mail e senha) — os dois são criados na mesma transação.
3. Use **Entrar como** na linha da empresa para assumir a sessão do admin dela; a partir daí você
   navega nas telas de domínio (`/`, `/clientes`, `/estoque`, `/configuracoes`) já com os dados
   daquela empresa.
4. Configure produtos em `/estoque/novo` (mutações de estoque e a página `/configuracoes` são
   restritas a administradores).
5. Para sair do modo empresa, use **Voltar à central** no menu do usuário.

Veja também [AGENTS.md](AGENTS.md) para as convenções de desenvolvimento, arquitetura e as
regras de multi-tenancy do projeto.