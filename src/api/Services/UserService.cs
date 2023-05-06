namespace WebAuthnTest.Api;

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Fido2NetLib;
using Fido2NetLib.Objects;
using WebAuthnTest.Database;

public class UserService
{
    private readonly WebAuthnTestDbContext _db;
    private readonly IFido2 _fido2;
    private readonly IMetadataService _fido2Mds;

    public UserService(
        WebAuthnTestDbContext db,
        IFido2 fido2,
        IMetadataService fido2Mds)
    {
        _db = db;
        _fido2 = fido2;
        _fido2Mds = fido2Mds;
    }

    /// <summary>
    /// Creates a new user in the database.
    /// </summary>
    /// <param name="user">The user to create.</param>
    /// <param name="userHandle">
    /// The user's handle. This corresponds to the User Handle in the WebAuthn spec. If null,
    /// a random handle will be generated.
    /// </param>
    /// <param name="credential">A login credential to add to the user, if any.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new user.</returns>
    public async Task<UserModel> CreateUserAsync(
        UserModelCreate user,
        byte[]? userHandle = null,
        UserCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        var userToAdd = new User
        {
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserHandle = userHandle ?? UserHandleConvert.NewUserHandle()
        };

        if (credential != null)
        {
            userToAdd.UserCredentials = new List<UserCredential> { credential };
        }

        var entry = _db.Users.Add(userToAdd);
        await _db.SaveChangesAsync(cancellationToken);

        return (await GetUserAsync(entry.Entity.Id, cancellationToken))!;
    }

    /// <summary>
    /// Gets a user from the database with the specified Id.
    /// </summary>
    /// <param name="userId">The user Id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user, or null if not found.</returns>
    public async Task<UserModel?> GetUserAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db
            .Users
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        return new UserModel
        {
            Id = user.Id,
            Created = user.Created,
            Updated = user.Updated,
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserHandle = user.UserHandle
        };
    }

    /// <summary>
    /// Updates the user with the specified Id.
    /// </summary>
    /// <param name="userId">The User's Id.</param>
    /// <param name="user">The updated user data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the user was updated, false if not found.</returns>
    public async Task<bool> UpdateUserAsync(
        long userId,
        UserModelUpdate user,
        CancellationToken cancellationToken = default)
    {
        var updatedUser = await _db
            .Users
            .AsTracking()
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        if (updatedUser == null)
        {
            return false;
        }

        updatedUser.DisplayName = user.DisplayName;
        updatedUser.FirstName = user.FirstName;
        updatedUser.LastName = user.LastName;

        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Gets the credential with the specified User Id and Credential Id.
    /// </summary>
    /// <param name="userId">The User's Id.</param>
    /// <param name="credentialId">The credential Id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The credential, or null if not found.</returns>
    public async Task<UserCredentialModel?> GetUserCredentialAsync(
        long userId,
        long credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await _db
            .UserCredentials
            .SingleOrDefaultAsync(
                t => t.UserId == userId && t.Id == credentialId,
                cancellationToken);

        if (credential == null)
        {
            return null;
        }

        return new UserCredentialModel
        {
            Id = credential.Id,
            Created = credential.Created,
            Updated = credential.Updated,
            UserId = credential.UserId,
            DisplayName = credential.DisplayName,
            AttestationFormatId = credential.AttestationFormatId
        };
    }

    /// <summary>
    /// Gets raw credential information with the specified, raw Credential Id, belonging to the user with
    /// the specified user handle. The raw credential includes the public key and other low-level information.
    /// </summary>
    /// <param name="userHandle">The user handle. This is the User Handle from the WebAuthn spec.</param>
    /// <param name="credentialId">The raw credential Id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw credential, or null if not found.</returns>
    public async Task<UserCredential?> GetUserCredentialRawAsync(
        byte[] userHandle,
        byte[] credentialId,
        CancellationToken cancellationToken = default)
    {
        return await _db
            .UserCredentials
            .SingleOrDefaultAsync(t =>
                t.User.UserHandle == userHandle && t.CredentialId == credentialId,
                cancellationToken);
    }

    /// <summary>
    /// Get a user's credentials.
    /// </summary>
    /// <param name="userId">The user's Id.</param>
    /// <returns>The user's credentials as an async enumerable.</returns>
    public IAsyncEnumerable<UserCredentialModel> GetUserCredentials(long userId)
    {
        return _db
            .UserCredentials
            .Where(t => t.UserId == userId)
            .Select(t => new UserCredentialModel
            {
                Id = t.Id,
                Created = t.Created,
                Updated = t.Updated,
                UserId = t.UserId,
                DisplayName = t.DisplayName,
                AttestationFormatId = t.AttestationFormatId
            })
            .OrderBy(t => t.Created)
            .AsAsyncEnumerable();
    }

    /// <summary>
    /// Deletes a credential from the specified user with the specified Credential Id.
    /// </summary>
    /// <param name="userId">The user's Id.</param>
    /// <param name="credentialId">The Id of the credential to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the credential was deleted, false if not found.</returns>
    public async Task<bool> DeleteUserCredentialAsync(
        long userId,
        long credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await _db
            .UserCredentials
            .Where(t => t.UserId == userId && t.Id == credentialId)
            .Select(t => new UserCredential
            {
                Id = t.Id,
                // Must retrieve hash because its an alternate key.
                // EF won't be able to track and subsequently delete without it.
                CredentialIdHash = t.CredentialIdHash
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (credential == null)
        {
            return false;
        }

        _db.UserCredentials.Remove(credential);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Gets the WebAuthn credential creation options used during the registration ceremony.
    /// </summary>
    /// <param name="user">The user participating in this registration ceremony.</param>
    /// <param name="existingCredentials">
    /// Any existing credentials for the user to be excluded from re-registration.
    /// Can be an empty collection.
    /// </param>
    /// <returns>The credential creation options.</returns>
    public CredentialCreateOptions GetCredentialCreateOptions(
        Fido2User user,
        IEnumerable<byte[]> existingCredentials)
    {
        var authenticatorSelection = new AuthenticatorSelection
        {
            UserVerification = UserVerificationRequirement.Required,
            RequireResidentKey = true
        };

        var exts = new AuthenticationExtensionsClientInputs()
        {
            Extensions = true,
            UserVerificationMethod = true,
        };

        var existingPublicKeyCredentials = existingCredentials
            .Select(t => new PublicKeyCredentialDescriptor(t))
            .ToList();

        return _fido2.RequestNewCredential(
            user,
            existingPublicKeyCredentials,
            authenticatorSelection,
            AttestationConveyancePreference.Direct,
            exts);
    }

    /// <summary>
    /// Adds a credential to the user with the specified Id.
    /// </summary>
    /// <param name="userId">The user's Id.</param>
    /// <param name="attestationResult">The credential data resulting from the completion of the registration ceremony.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The added credential.</returns>
    public async Task<UserCredentialModel> SaveCredentialAsync(
        long userId,
        AttestationVerificationSuccess attestationResult,
        CancellationToken cancellationToken = default)
    {
        var credential = await GenerateCredentialRawAsync(attestationResult, cancellationToken);
        credential.UserId = userId;

        var entry = _db.UserCredentials.Add(credential);
        await _db.SaveChangesAsync(cancellationToken);

        //TODO: Delete existing credential if it has same raw credential Id?

        return (await GetUserCredentialAsync(userId, entry.Entity.Id, cancellationToken))!;
    }

    /// <summary>
    /// Gets the WebAuthn credential assertion options used during the authentication ceremony.
    /// </summary>
    /// <param name="allowedCredentials">
    /// Credentials allowed for this authentication ceremony. If empty, there will be no restrictions
    /// on which credential can be used to authenticate.
    /// </param>
    /// <returns>The credential assertion options.</returns>
    public AssertionOptions GetCredentialAssertionOptions(
        IEnumerable<byte[]> allowedCredentials)
    {
        var exts = new AuthenticationExtensionsClientInputs()
        {
            UserVerificationMethod = true
        };

        var allowedPublicKeyCredentials = allowedCredentials
            .Select(t => new PublicKeyCredentialDescriptor(t))
            .ToList();

        return _fido2.GetAssertionOptions(
            allowedPublicKeyCredentials,
            UserVerificationRequirement.Required,
            exts);
    }

    /// <summary>
    /// Generates a raw credential using the authenticator device's attestation info.
    /// </summary>
    /// <param name="attestationResult">The credential data resulting from the completion of the registration ceremony.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new, raw credential.</returns>
    public async Task<UserCredential> GenerateCredentialRawAsync(
        AttestationVerificationSuccess attestationResult,
        CancellationToken cancellationToken = default)
    {
        var credentialDisplayName = await GenerateCredentialDisplayNameAsync(
            attestationResult,
            cancellationToken);

        var credential = new UserCredential
        {
            CredentialId = attestationResult.CredentialId,
            PublicKey = attestationResult.PublicKey,
            AttestationFormatId = attestationResult.CredType,
            AaGuid = attestationResult.Aaguid,
            DisplayName = credentialDisplayName,
            SignatureCounter = attestationResult.Counter
        };

        using (var hash = SHA512.Create())
        {
            credential.CredentialIdHash = hash.ComputeHash(credential.CredentialId);
        }

        return credential;
    }

    /// <summary>
    /// Generates a credential display name using the authenticator device's attestation info.
    /// </summary>
    /// <param name="attestationResult">The credential data resulting from the completion of the registration ceremony.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The display name.</returns>
    /// <remarks>
    /// By default, the display name is pulled from the FIDO2 metadata service using the authenticator
    /// device's AAGUID.
    /// </remarks>
    private async Task<string> GenerateCredentialDisplayNameAsync(
        AttestationVerificationSuccess attestationResult,
        CancellationToken cancellationToken = default)
    {
        var authenticatorMetadata = await _fido2Mds.GetEntryAsync(attestationResult.Aaguid, cancellationToken);
        var authenticatorDescription = authenticatorMetadata?.MetadataStatement.Description;
        var credentialDisplayName = authenticatorDescription ?? attestationResult.CredType;

        return credentialDisplayName.Truncate(UserCredentialConfiguration.DISPLAY_NAME_MAX_LENGTH);
    }
}
