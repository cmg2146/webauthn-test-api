namespace WebAuthnTest.Database;

public static class UserHandleConvert
{
    /// <summary>
    /// Generates a new User Handle from a Guid
    /// </summary>
    /// <returns>The User Handle</returns>
    public static byte[] NewUserHandle()
    {
        return Guid.NewGuid().ToByteArray();
    }
}
