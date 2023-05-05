namespace WebAuthnTest.Api;

using System.Globalization;
using System.Security.Claims;
using System.Security.Principal;
using WebAuthnTest.Database;

public static class ClaimsIdentityExtensions
{
    /// <summary>
    /// Retrieves the User Id from the user's IIdentity.
    /// </summary>
    public static long UserId(this IIdentity identity)
    {
        return long.Parse(identity.Name!, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Adds WebAuthn related claims to the ClaimsIdentity.
    /// </summary>
    /// <param name="identity">The ClaimsIdentity to add claims to.</param>
    /// <param name="credential">The WebAuthn credential to populate claims from.</param>
    /// <remarks>
    /// This method is not indempotent. It must only be called once when initially
    /// creating the ClaimsIdentity.
    /// </remarks>
    public static void AddWebAuthnClaims(this ClaimsIdentity identity, UserCredential credential)
    {
        identity.AddClaim(new Claim(
            ClaimTypes.AuthenticationMethod,
            WebAuthnClaimConstants.AuthenticationMethod
        ));
        identity.AddClaim(new Claim(
            WebAuthnClaimConstants.UserCredentialIdClaimType,
            $"{credential.Id}",
            ClaimValueTypes.UInteger64
        ));
    }

    /// <summary>
    /// Retrieves the Id of the WebAuthn credential used to authenticate the current user.
    /// </summary>
    /// <param name="identity">The ClaimsIdentity representing the current user.</param>
    public static long? GetActiveCredentialId(this ClaimsIdentity? identity)
    {
        var credentialIdClaim = identity?.FindFirst(WebAuthnClaimConstants.UserCredentialIdClaimType);

        if (credentialIdClaim == null)
        {
            return null;
        }

        if (!long.TryParse(credentialIdClaim.Value, out var result))
        {
            return null;
        }

        return result;
    }
}
