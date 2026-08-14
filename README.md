# André Gas e Água

Sistema de controle para revendedora de gás e água — clientes (com controle de fiado/saldo
devedor), estoque, vendas e dashboards gerenciais.

## Stack
- **.NET 10** / C#
- **.NET Aspire** — orquestração local (Postgres, dashboard de telemetria)
- **Blazor Web App** (Interactive Server) + **MudBlazor** — front-end
- **EF Core** + **PostgreSQL** — persistência
- **ASP.NET Core Identity** — autenticação
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
Pré-requisitos: .NET 10 SDK (ver [global.json](/home/voch_silva/dev/opencode/andregas/global.json)) e Docker (para o container do Postgres via Aspire).

```bash
dotnet run --project src/AndreGas.AppHost
```

Isso abre o **Aspire Dashboard** com os logs/telemetria de todos os recursos, incluindo o
container do Postgres e a aplicação web.

## Testes
```bash
dotnet test
```

Veja também [AGENTS.md](/home/voch_silva/dev/opencode/andregas/AGENTS.md) para as convenções de desenvolvimento e arquitetura do projeto.
