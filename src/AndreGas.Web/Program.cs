using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;
using AndreGas.Web.Common.Behaviors;
using AndreGas.Web.Components;
using AndreGas.Web.Features.Auth;
using AndreGas.Web.Features.Auth.Login;
using AndreGas.Web.Features.Seed;
using AndreGas.Web.Features.Usuarios.Cadastrar;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Default nacional do processo: textos gerados no servidor fora de request (auditoria de
// histórico, seed) formatam R$/dd-MM com pt-BR mesmo sem HttpContext ativo.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("andregas");

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies(options => options.ApplicationCookie!.Configure(cookie =>
    {
        // Redirect unauthenticated requests to our custom /login page instead of the
        // Identity UI default ("/Account/Login").
        cookie.LoginPath = "/login";
        // Redirect a usuário autenticado, porém sem a role exigida, para a página personalizada
        // de "Sem Autorização" (em vez do comportamento padrão de 403/redirect ao login).
        cookie.AccessDeniedPath = "/sem-autorizacao";
    }));

// Every page requires an authenticated user unless explicitly marked [AllowAnonymous]
// (e.g. the login page).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    Politicas.AdicionarPoliticas(options);
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<IPasswordSignIn, IdentityPasswordSignIn>();
builder.Services.AddScoped<IUsuarioAutenticado, UsuarioAutenticado>();
builder.Services.AddScoped<IUsuarioWriter, IdentityUsuarioWriter>();
builder.Services.AddScoped<VendasAtualizadasNotifier>();
builder.Services.AddSingleton(TimeProvider.System);

// Globalização: app interno brasileiro → força pt-BR (R$, dd/MM/yyyy) para todos os requests,
// sem negociar com o navegador. Sem isso, em produção o SO do container (UTC/en) renderizaria
// "US$" e datas MM/dd.
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var ptBrasil = new CultureInfo("pt-BR");
    options.DefaultRequestCulture = new RequestCulture(ptBrasil, ptBrasil);
    options.SupportedCultures = [ptBrasil];
    options.SupportedUICultures = [ptBrasil];
    // Remove qualquer provedor baseado no navegador — sempre pt-BR.
    options.RequestCultureProviders.Clear();
});

builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

var app = builder.Build();

app.MapDefaultEndpoints();

// Applies pending EF Core migrations automatically on startup, and seeds a default admin user
// so there is always a working login — simple approach suited to this app's scale.
// SeedDemoData (dados de demonstração) roda SOMENTE em ambientes não-Produção — em produção
// desligado via "Seed:EnableDemoData": false (appsettings.Production.json).
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var credentialsRequired = app.Environment.IsProduction();
    await SeedData.SeedDefaultAdminUserAsync(scope.ServiceProvider, app.Configuration, requireExplicitCredentials: credentialsRequired);

    if (app.Configuration.GetValue("Seed:EnableDemoData", !app.Environment.IsProduction()))
    {
        await SeedDemoData.SeedAsync(dbContext, scope.ServiceProvider.GetRequiredService<TimeProvider>());
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Aplica a cultura pt-BR para requests SSR e circuitos interativos do Blazor Server.
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAuthEndpoints();

app.Run();
