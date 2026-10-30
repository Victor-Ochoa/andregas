using Mediator;

namespace RevendaApp.Web.Features.Central.InativarEmpresa;

/// <summary>
/// Inativa ou reativa uma empresa pela central. Não existe exclusão física: a empresa inativa
/// continua no banco (com todos os seus dados) e apenas perde o acesso — o
/// <c>LoginCommandHandler</c> barra o login dos seus usuários enquanto ela estiver desativada.
/// </summary>
/// <param name="EmpresaId">Empresa a (re)ativar.</param>
/// <param name="Ativar"><c>true</c> reativa; <c>false</c> inativa.</param>
public sealed record InativarEmpresaCommand(Guid EmpresaId, bool Ativar) : ICommand<bool>;