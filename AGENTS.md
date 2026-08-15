# AGENTS.md — Contexto para agentes de IA

Este arquivo orienta qualquer agente de IA (Copilot, Claude, Cursor, etc.) trabalhando neste
repositório. Leia isto antes de propor ou aplicar mudanças.

## Sobre o projeto
"André Gas e Água" é uma revendedora de gás de cozinha e água mineral. O sistema controla:
clientes (incluindo fiado/saldo devedor), estoque de produtos, registro de vendas e dashboards
gerenciais (vendas diárias/mensais, estoque, lucro). Cores da marca: **azul marinho e branco**.

## Stack e decisões de arquitetura
- **.NET 10**, C# — SDK fixado em [global.json](/home/voch_silva/dev/opencode/andregas/global.json).
- **.NET Aspire** orquestra o ambiente local: Postgres roda como container gerenciado pelo
  `AndreGas.AppHost`. Não configure connection strings manuais — use `builder.AddPostgres(...)` /
  `builder.AddNpgsqlDbContext<AppDbContext>(...)` do Aspire.
- **Blazor Web App** com render mode **Interactive Server** (não WebAssembly, não Auto). Não há
  projeto de API separado — os componentes Razor chamam os handlers diretamente via DI.
- **MudBlazor** para todos os componentes de UI. Tema customizado azul marinho + branco definido
  uma única vez (ver `MudTheme` em `AndreGas.Web`).
- **Vertical Slice Architecture (VSA)**: funcionalidades ficam em `AndreGas.Web/Features/<Area>/<CasoDeUso>/`,
  cada uma contendo, juntos: Command/Query, Handler, Validator (FluentValidation) e o(s)
  componente(s) `.razor`. **Não** criar camadas horizontais genéricas (`Services/`, `Controllers/`,
  `Repositories/` compartilhados) — cada slice acessa o `AppDbContext` diretamente dentro do seu
  handler.
- **Mediator** (pacote `Mediator.Abstractions` / `Mediator.SourceGenerator`, autor martinothamar) é
  o mediator usado para os casos de uso — **não usar MediatR v13+** (tornou-se pago/comercial).
  API é quase idêntica a MediatR (`IRequest<T>`, `IRequestHandler<TRequest, TResponse>`).
  Registrado com `AddMediator(o => o.ServiceLifetime = ServiceLifetime.Scoped)` — **não** usar o
  lifetime padrão (`Singleton`), pois handlers costumam depender de serviços scoped do EF
  Core/Identity (ex.: `SignInManager`), o que quebra a validação de DI em tempo de execução.
  Validação automática via `AndreGas.Web/Common/Behaviors/ValidationBehavior.cs` (pipeline
  behavior que roda qualquer `IValidator<TMessage>` do FluentValidation antes do handler).
- **EF Core** + **PostgreSQL** via `Npgsql.EntityFrameworkCore.PostgreSQL`. Migrations vivem em
  `AndreGas.Infrastructure`.
- **ASP.NET Core Identity** para autenticação, com `ApplicationUser` e **papel único** (sem
  roles/permissões diferenciadas por enquanto). `CookieAuthenticationOptions.LoginPath` é
  configurado para `/login` (via `AddIdentityCookies(o => o.ApplicationCookie.Configure(...))`).
  Um usuário administrador padrão é criado automaticamente na inicialização (ver
  `Features/Auth/SeedData.cs`; credenciais configuráveis via `SeedAdmin:Email`/`SeedAdmin:Password`).
- **Páginas de autenticação (Login) exigem SSR estático**: qualquer página Razor que precise
  gravar o cookie de autenticação via `SignInManager` (ex.: Login) **não pode** usar
  `@rendermode InteractiveServer` — o `HttpContext` só está disponível como
  `[CascadingParameter]` durante SSR estático. Por isso `Login.razor` não declara `@rendermode` e
  usa um layout próprio (`LoginLayout.razor`) em vez do `MainLayout`. Consequência importante:
  **os componentes de formulário do MudBlazor (`MudTextField`, `MudCheckBox` etc.) não funcionam
  em páginas SSR estáticas** (dependem de interatividade/JS interop) — use os componentes nativos
  do Blazor (`InputText`, `InputCheckbox`, `EditForm` + `DataAnnotationsValidator`) para qualquer
  campo de formulário em página estática; componentes MudBlazor puramente apresentacionais
  (`MudPaper`, `MudText`, `MudAlert`, `MudButton` com `ButtonType.Submit`) funcionam normalmente.
  Páginas comuns (dashboards, listas, modais) devem declarar `@rendermode InteractiveServer`
  (ver `Home.razor`) para poderem usar os componentes interativos do MudBlazor normalmente.
- **Central Package Management**: todas as versões de pacotes NuGet ficam em
  [Directory.Packages.props](/home/voch_silva/dev/opencode/andregas/Directory.Packages.props) na
  raiz. Os `.csproj` referenciam pacotes **sem** atributo `Version`.
- **Migrations EF Core**: geradas a partir de `AndreGas.Infrastructure` (tem seu próprio
  `IDesignTimeDbContextFactory` em `AppDbContextFactory.cs`, usado só em tempo de design — em
  runtime a connection string vem do Aspire). Requer a ferramenta `dotnet-ef` instalada
  (`dotnet tool install --global dotnet-ef`). Para gerar uma nova migração:
  `dotnet-ef migrations add NomeDaMigracao --project src/AndreGas.Infrastructure --startup-project src/AndreGas.Infrastructure -o Migrations`.
  As migrações são aplicadas automaticamente na inicialização do `AndreGas.Web` (`Database.MigrateAsync()`).

## TDD — fluxo obrigatório

Este projeto é desenvolvido com TDD. Para qualquer novo caso de uso/handler:
1. Escreva primeiro o teste (unitário para lógica de handler/validator com mocks/InMemory;
   de integração quando envolver fluxo completo contra Postgres real via `Aspire.Hosting.Testing`).
2. Rode o teste e confirme que falha.
3. Implemente o mínimo necessário para o teste passar.
4. Refatore mantendo os testes verdes.
- Testes unitários: `tests/AndreGas.UnitTests`. Validators e handlers que usam `AppDbContext`
  são testados com o provider **EF Core InMemory** (`AndreGas.UnitTests.TestHelpers.InMemoryDbContextFactory`)
  — rápido e sem depender de container, mas suficiente para validar a lógica dos handlers.
- Testes de integração: `tests/AndreGas.IntegrationTests` — sobem o Postgres real via Aspire
  (nada de mocks de banco em testes de integração). Duas fixtures reutilizáveis (`IClassFixture`):
  - `WebAppFixture`: sobe a distribuição inteira (Postgres + `AndreGas.Web`) e expõe um
    `HttpClient` por teste — usada para fluxos ponta a ponta via HTTP (ex.: `LoginFlowTests`).
  - `DatabaseFixture`: sobe **só** o recurso Postgres do mesmo `AndreGas.AppHost` (sem subir o
    processo `AndreGas.Web`) e expõe `CreateDbContext()` — mais rápida, usada para testar
    handlers diretamente em processo (ex.: `ClientesFeatureTests`, `EstoqueFeatureTests`).
  - A suíte roda com `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
    (`AssemblyInfo.cs`) porque cada classe de teste sobe seu próprio container Postgres —
    rodar em paralelo sobrecarrega o ambiente e causa falhas transitórias de conexão.
  - **Gotcha**: o Postgres do AppHost usa `.WithDataVolume()` (dados persistem entre execuções,
    intencional para `dotnet run` local). Se os testes de integração começarem a **travar/dar
    timeout na inicialização da fixture** sem motivo aparente (ex.: após uma execução de testes
    interrompida no meio), o volume Docker pode ter ficado travado/corrompido — resolva com
    `docker volume rm andregas.apphost-<hash>-postgres-data` (veja o nome exato com
    `docker volume ls`) e rode os testes novamente.

## Modelo de domínio (regras de negócio essenciais)
- **Cliente**: `Telefone` é a **chave natural única** — é assim que o operador localiza o cliente
  na tela de venda. `Endereco` é obrigatório. `SaldoDevedor` começa zerado.
- **Produto**: tem três preços — `PrecoVenda` (normal), `PrecoCusto` (para cálculo de lucro) e
  `PrecoGasDoPovo` (preço subsidiado, usado quando a forma de pagamento é "Gás do Povo").
- **Formas de pagamento** (`FormaPagamento`): `Pix`, `Debito`, `Credito`, `Dinheiro`, `Fiado`,
  `GasDoPovo`.
  - `Fiado` → soma o `ValorTotal` da venda ao `SaldoDevedor` do cliente.
  - `GasDoPovo` → pago integralmente no ato (**não** gera saldo devedor), mas usa
    `Produto.PrecoGasDoPovo` como preço unitário dos itens em vez de `PrecoVenda`.
  - Demais formas → pagas no ato, sem afetar o saldo devedor.
- **Desconto na venda**: campo único por venda (não por item), começa zerado, opcional.
  `ValorTotal = ValorBruto − Desconto`. É o `ValorTotal` (já líquido) que é somado ao saldo
  devedor quando `Fiado`. O desconto é **rateado proporcionalmente entre os itens**, reduzindo o
  `Lucro` líquido de cada item (mantém-se também um `LucroBruto` de referência sem desconto).
- **Lucro**: por item = `(PrecoUnitario − PrecoCustoUnitario) × Quantidade` menos a parcela
  rateada do desconto. Exibido no detalhe da venda, no histórico do cliente e agregado nos
  gráficos de lucro do dashboard.
- **Fluxo de Nova Venda** (modal global acessível pelo menu superior em todas as telas):
  1. Operador digita o telefone do cliente.
  2. Se existe → carrega nome/endereço/saldo automaticamente (somente leitura nesta tela).
  3. Se não existe → formulário inline exige Nome, Telefone e Endereço (todos obrigatórios) e
     cadastra o cliente com saldo zerado.
  4. Operador adiciona produtos/quantidades e, opcionalmente, um desconto.
  5. Ao confirmar: cria `Venda` + `ItemVenda`(s), baixa estoque (`MovimentacaoEstoque` tipo Saída),
     e atualiza `SaldoDevedor` do cliente somente se `FormaPagamento == Fiado`.

## Convenções de código
- Nomes de domínio, telas e mensagens ao usuário em **português** (é o idioma do negócio); nomes
  de tipos/namespaces em C# seguem PascalCase normal do .NET.
- Nullable reference types habilitado; evite `null!` — prefira modelagem explícita.
- Cada slice deve ter testes antes da implementação (ver seção TDD acima).
- Após cada fase de desenvolvimento concluída e validada, gerar um commit com mensagem no padrão
  [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, `test:`).

## O que evitar
- Não usar MediatR versão 13 ou superior (pago). Usar o pacote `Mediator` (martinothamar).
- Não criar camadas de repositório genéricas compartilhadas entre slices.
- Não hardcodar connection strings — sempre via Aspire.
- Não implementar roles/permissões de usuário (fora de escopo por decisão do usuário).
