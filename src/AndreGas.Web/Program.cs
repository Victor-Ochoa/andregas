using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Identity;
using AndreGas.Web.Common.Behaviors;
using AndreGas.Web.Components;
using AndreGas.Web.Features.Auth;
using AndreGas.Web.Features.Auth.Login;
using AndreGas.Web.Features.Seed;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("andregas");

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies(options => options.ApplicationCookie!.Configure(cookie =>
    {
        // Redirect unauthenticated requests to our custom /login page instead of the
        // Identity UI default ("/Account/Login").
        cookie.LoginPath = "/login";
    }));

// Every page requires an authenticated user unless explicitly marked [AllowAnonymous]
// (e.g. the login page).
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<IPasswordSignIn, IdentityPasswordSignIn>();
builder.Services.AddScoped<AndreGas.Web.Common.VendasAtualizadasNotifier>();
builder.Services.AddSingleton(TimeProvider.System);

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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAuthEndpoints();

app.Run();
