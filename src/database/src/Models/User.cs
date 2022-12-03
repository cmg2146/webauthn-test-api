namespace WebAuthnTest.Database;

public class User : Entity
{
    public string DisplayName { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;

    public IEnumerable<UserCredential> UserCredentials { get; set; } = new List<UserCredential>();
}
