namespace WebAuthnTest.Api;

public class UserCredentialModel : ModelBase
{
    public long UserId { get; set; }
    public string AttestationFormatId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
}
