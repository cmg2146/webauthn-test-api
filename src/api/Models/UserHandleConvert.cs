namespace WebAuthnTest.Api;

using System.Buffers.Binary;

public static class UserHandleConvert
{
    /// <summary>
    /// Converts a User Handle to a User Id
    /// </summary>
    /// <param name="userHandle">The User Handle from the authenticator device</param>
    /// <returns>The User Id</returns>
    public static long ToUserId(byte[] userHandle)
    {
        return BinaryPrimitives.ReadInt64BigEndian(userHandle);
    }

    /// <summary>
    /// Converts a User Id to a User Handle
    /// </summary>
    /// <param name="userId">The User Id</param>
    /// <returns>The User Handle</returns>
    public static byte[] ToUserHandle(long userId)
    {
        var handle = BitConverter.GetBytes(userId);

        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.WriteInt64BigEndian(handle, userId);
        }

        return handle;
    }
}
