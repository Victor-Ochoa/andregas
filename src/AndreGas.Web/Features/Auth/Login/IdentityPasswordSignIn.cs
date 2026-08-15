using AndreGas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace AndreGas.Web.Features.Auth.Login;

/// <summary>Implementação de <see cref="IPasswordSignIn"/> baseada no Identity <see cref="SignInManager{TUser}"/>.</summary>
public sealed class IdentityPasswordSignIn(SignInManager<ApplicationUser> signInManager) : IPasswordSignIn
{
    public async Task<SignInOutcome> PasswordSignInAsync(string email, string password, bool rememberMe)
    {
        var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return SignInOutcome.Succeeded;
        }

        if (result.IsLockedOut)
        {
            return SignInOutcome.LockedOut;
        }

        if (result.IsNotAllowed)
        {
            return SignInOutcome.NotAllowed;
        }

        return SignInOutcome.Failed;
    }
}
