namespace WebAuthnTest.Api;

public static class StringExtensions
{
    public static string Truncate(this string input, int length)
    {
        return input.Length > length
            ? input.Substring(0, length)
            : input;
    }
}