using Mediator;

namespace RevendaApp.Web.Features.Central.EntrarComo;

/// <summary>
/// "Entrar como": o super administrador da central assume a sessão do <b>admin de uma empresa</b>
/// (impersonation). A empresa é escolhida na central; o usuário alvo não é escolhido nesta task.
/// </summary>
/// <param name="EmpresaId">Id da empresa em que o super admin quer entrar.</param>
public sealed record EntrarComoCommand(Guid EmpresaId) : ICommand<EntrarComoResult>;