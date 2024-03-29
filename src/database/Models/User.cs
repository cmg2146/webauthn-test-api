namespace WebAuthnTest.Database;

public class User : Entity
{
    public string DisplayName { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public byte[] UserHandle { get; set; } = null!;

    public IEnumerable<UserCredential> UserCredentials { get; set; } = null!;
}
