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

    public CredentialCreateOptions GetCredentialCreateOptions(
        UserModel user,
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

        var fido2User = new Fido2User
        {
            Id = user.UserHandle,
            Name = user.DisplayName,
            DisplayName = $"{user.FirstName} {user.LastName}"
        };

        return _fido2.RequestNewCredential(
            fido2User,
            existingPublicKeyCredentials,
            authenticatorSelection,
            AttestationConveyancePreference.Direct,
            exts);
    }

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

    public async Task<UserCredential> GenerateCredentialAsync(
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
