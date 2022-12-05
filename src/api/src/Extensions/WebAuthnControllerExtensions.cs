namespace WebAuthnTest.Api;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using WebAuthnTest.Database;

public static class WebAuthnControllerExtensions
{
    public static SignInResult SignInWithUserCredential(
        this WebAuthnController controller,
        UserCredential credential)
    {
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);

        identity.AddClaim(new Claim(
            identity.NameClaimType,
            $"{credential.UserId}",
            ClaimValueTypes.UInteger64
        ));

        identity.AddClaim(new Claim(
            ClaimTypes.AuthenticationMethod,
            WebAuthnClaimConstants.WEBAUTHN_AUTHENTICATION_METHOD
        ));

        identity.AddClaim(new Claim(
            WebAuthnClaimConstants.USER_CREDENTIAL_ID_CLAIM_TYPE,
            $"{credential.Id}",
            ClaimValueTypes.UInteger64
        ));

        return controller.SignIn(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
