using Mediator;

namespace AndreGas.Web.Features.Central.VoltarCentral;

/// <summary>
/// "Voltar à central": encerra a impersonation e reemite o cookie do super admin que a iniciou.
/// </summary>
/// <remarks>
/// O comando <b>não</b> carrega o id do super admin: ele é lido exclusivamente da claim assinada
/// <c>SuperAdminOriginalId</c> do cookie (ver <see cref="VoltarCentralCommandHandler"/>). Aceitar o
/// id do corpo/query permitiria que qualquer usuário autenticado se tornasse super admin.
/// </remarks>
public sealed record VoltarCentralCommand : ICommand<VoltarCentralResult>;