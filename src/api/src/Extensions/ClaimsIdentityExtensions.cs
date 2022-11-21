namespace WebAuthnTest.Api;

using System.Security.Claims;
using System.Security.Principal;

public static class ClaimsIdentityExtensions
{
    public static long UserId(this IIdentity identity)
    {
        return long.Parse(identity.Name!);
    }
}