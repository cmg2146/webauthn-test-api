namespace WebAuthnTest.Api;

public interface IUserModelBase
{
    string DisplayName { get; set; }
    string FirstName { get; set; }
    string LastName { get; set; }
}
