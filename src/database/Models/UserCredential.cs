namespace WebAuthnTest.Database;

public class UserCredential : Entity
{
    public long UserId { get; set; }
    /// <summary>
    /// Credential ID created by the authenticator device
    /// </summary>
    public byte[] CredentialId { get; set; } = null!;
    /// <summary>
    /// Credential ID Hash. Need the Credential ID to be unique, but it's too big to index, so we
    /// hash it first and index the hash
    /// </summary>
    public byte[] CredentialIdHash { get; set; } = null!;
    /// <summary>
    /// Public key created by the authenticator device
    /// </summary>
    public byte[] PublicKey { get; set; } = null!;
    /// <summary>
    /// The attestation statement format identifier from the authenticator device
    /// </summary>
    public string AttestationFormatId { get; set; } = null!;
    public Guid AaGuid { get; set; }
    public string DisplayName { get; set; } = null!;
    public uint SignatureCounter { get; set; }

    public User User { get; set; } = null!;
}
