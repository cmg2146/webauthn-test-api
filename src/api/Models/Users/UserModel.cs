namespace WebAuthnTest.Api;

public class UserModel : ModelBase, IUserModelBase
{
    public string DisplayName { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public byte[] UserHandle { get; set; } = null!;
}
