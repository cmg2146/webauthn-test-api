namespace WebAuthnTest.Api;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using WebAuthnTest.Database;

public static class WebAuthnControllerExtensions
{
    /// <summary>
    /// Signs the user into the specified authentication scheme using a WebAuthn credential.
    /// </summary>
    /// <param name="controller">The WebAuthnController Controller instance.</param>
    /// <param name="credential">The WebAuthn credential to sign in with.</param>
    /// <param name="authenticationScheme">The authentication scheme to sign the user into.</param>
    public static SignInResult SignInWithUserCredential(
        this WebAuthnController controller,
        UserCredential credential,
        string authenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme)
    {
        var identity = new ClaimsIdentity(authenticationScheme);

        identity.AddClaim(new Claim(
            identity.NameClaimType,
            $"{credential.UserId}",
            ClaimValueTypes.UInteger64
        ));

        identity.AddWebAuthnClaims(credential);

        return controller.SignIn(new ClaimsPrincipal(identity), authenticationScheme);
    }
}
