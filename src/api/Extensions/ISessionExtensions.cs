namespace WebAuthnTest.Api;

public static class ISessionExtensions
{
    public static async Task<string?> GetStringAsync(
        this ISession session,
        string key,
        CancellationToken cancellationToken = default)
    {
        await session.LoadAsync(cancellationToken);
        return session.GetString(key);
    }

    public static async Task SetStringAsync(
        this ISession session,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        await session.LoadAsync(cancellationToken);
        session.SetString(key, value);
        await session.CommitAsync(cancellationToken);
    }
}
