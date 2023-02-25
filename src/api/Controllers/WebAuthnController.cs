namespace WebAuthnTest.Api;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Fido2NetLib;
using Fido2NetLib.Objects;
using WebAuthnTest.Database;

[ApiController]
[Route("webauthn")]
[Produces("application/json")]
public class WebAuthnController : Controller
{
    private readonly IFido2 _fido2;
    private readonly IMetadataService _fido2Mds;
    private readonly WebAuthnTestDbContext _db;

    public WebAuthnController(
        WebAuthnTestDbContext db,
        IFido2 fido2,
        IMetadataService fido2Mds)
    {
        _db = db;
        _fido2 = fido2;
        _fido2Mds = fido2Mds;
    }

    private static string FormatException(Exception e)
    {
        return e.InnerException?.Message == null
            ? e.Message
            : $"{e.Message} ({e.InnerException.Message})";
    }

    /// <summary>
    /// Begin device registration - retrieve credential creation options to start WebAuthn registration ceremony
    /// </summary>
    /// <returns>The credential creation options</returns>
    /// <response code="200">Returns the options</response>
    /// <response code="500">Problem generating options</response>
    [HttpGet("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CredentialCreateOptions>> GetCredentialCreationOptionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.Identity!.UserId();

        var user  = await _db
            .Users
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        //should never happen
        if (user == null)
        {
            return Unauthorized();
        }

        try
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

            var fido2User = new Fido2User
            {
                Id = BitConverter.GetBytes((long)0),
                Name = user.DisplayName,
                DisplayName = $"{user.FirstName} {user.LastName}"
            };

            BinaryPrimitives.WriteInt64BigEndian(fido2User.Id, user.Id);

            var existingCredentials = await _db
                .UserCredentials
                .Where(t => t.UserId == userId)
                .Select(t => new PublicKeyCredentialDescriptor(t.CredentialId))
                .ToListAsync(cancellationToken);

            var credentialCreationOptions = _fido2
                .RequestNewCredential(
                    fido2User,
                    existingCredentials,
                    authenticatorSelection,
                    AttestationConveyancePreference.Direct,
                    exts);

            HttpContext.Session.SetString("webAuthn.credentialCreateOptions", credentialCreationOptions.ToJson());

            return credentialCreationOptions;
        }
        catch (Exception e)
        {
            return Problem(FormatException(e), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Complete device registration - post authenticator attestation response to complete WebAuthn registration ceremony
    /// </summary>
    /// <returns>The new credential info</returns>
    /// <response code="200">Returns the new credential info</response>
    /// <response code="401">There was an issue validating the authenticator attestation response</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Fido2.CredentialMakeResult>> CreateCredentialAsync(
        AuthenticatorAttestationRawResponse attestationResponse,
        CancellationToken cancellationToken)
    {
        var userId = User.Identity!.UserId();
        Fido2.CredentialMakeResult credentialCreateResult;

        try
        {
            //get the credential creation options we originally sent to client
            var originalCreationOptions = CredentialCreateOptions
                .FromJson(HttpContext.Session.GetString("webAuthn.credentialCreateOptions"));

            credentialCreateResult = await _fido2
                .MakeNewCredentialAsync(
                    attestationResponse,
                    originalCreationOptions,
                    //we have a unique index in the database so this would be redundant
                    async (args, _) => await Task.FromResult(true),
                    cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            return Unauthorized(FormatException(e));
        }

        var aaGuid = credentialCreateResult.Result!.Aaguid;
        var authenticatorMetadata = await _fido2Mds.GetEntryAsync(aaGuid, cancellationToken);
        var authenticatorDescription = authenticatorMetadata
            ?.MetadataStatement
            .Description
            .Truncate(255);

        //TODO: Delete existing credential if it has same Id?
        var userCredentialToAdd = new UserCredential
        {
            UserId = userId,
            CredentialId = credentialCreateResult.Result.CredentialId,
            PublicKey = credentialCreateResult.Result.PublicKey,
            AttestationFormatId = credentialCreateResult.Result.CredType,
            AaGuid = aaGuid,
            DisplayName = authenticatorDescription ?? credentialCreateResult.Result.CredType,
            SignatureCounter = credentialCreateResult.Result.Counter
        };

        using (var hash = SHA512.Create())
        {
            userCredentialToAdd.CredentialIdHash = hash.ComputeHash(userCredentialToAdd.CredentialId);
        }

        var entry = _db.UserCredentials.Add(userCredentialToAdd);

        await _db.SaveChangesAsync(cancellationToken);

        // System.Text.Json cannot serialize these properly. See https://github.com/passwordless-lib/fido2-net-lib/issues/328
        credentialCreateResult.Result.AttestationCertificate = null;
        credentialCreateResult.Result.AttestationCertificateChain = null;

        return credentialCreateResult;
    }

    /// <summary>
    /// Begin sign in - retrieve assertion options to start WebAuthn authentication ceremony
    /// </summary>
    /// <returns>The assertion options</returns>
    /// <response code="200">Returns assertion options</response>
    /// <response code="500">Problem generating options</response>
    [AllowAnonymous]
    [HttpGet("authenticate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AssertionOptions> GetCredentialRequestOptions()
    {
        try
        {
            var exts = new AuthenticationExtensionsClientInputs()
            {
                UserVerificationMethod = true
            };

            var credentialAssertionOptions = _fido2.GetAssertionOptions(
                new List<PublicKeyCredentialDescriptor>(),
                UserVerificationRequirement.Required,
                exts
            );

            HttpContext.Session.SetString("webAuthn.credentialAssertionOptions", credentialAssertionOptions.ToJson());

            return credentialAssertionOptions;
        }

        catch (Exception e)
        {
            return Problem(FormatException(e), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Complete sign in - post authenticator assertion response to complete WebAuthn authentication ceremony
    /// </summary>
    /// <returns>Signs the user in by issuing an authentication cookie</returns>
    /// <response code="200">Successful sign in</response>
    /// <response code="401">There was an issue validating the authenticator assertion response</response>
    [AllowAnonymous]
    [HttpPost("authenticate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AuthenticateCredentialAsync(
        AuthenticatorAssertionRawResponse assertionResponse,
        CancellationToken cancellationToken)
    {
        var userId = BinaryPrimitives.ReadInt64BigEndian(assertionResponse.Response.UserHandle);

        var user = await _db
            .Users
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        if (user == null)
        {
            return Unauthorized();
        }

        var credential = await _db
            .UserCredentials
            .AsTracking()
            .SingleOrDefaultAsync(t =>
                t.UserId == userId && t.CredentialId == assertionResponse.Id,
                cancellationToken);

        if (credential == null)
        {
            return Unauthorized();
        }

        try
        {
            // get the assertion options we orignally sent the client
            var originalRequestOptions = AssertionOptions.FromJson(
                HttpContext.Session.GetString("webAuthn.credentialAssertionOptions"));

            var assertionVerificationResult = await _fido2
                .MakeAssertionAsync(
                    assertionResponse,
                    originalRequestOptions,
                    credential.PublicKey,
                    credential.SignatureCounter,
                    async (args, _) => await Task.FromResult(credential.UserId == userId),
                    cancellationToken: cancellationToken);

            // update the counter
            credential.SignatureCounter = assertionVerificationResult.Counter;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            return Unauthorized(FormatException(e));
        }

        return this.SignInWithUserCredential(credential);
    }

    /// <summary>
    /// Register new user and signs the new user in
    /// </summary>
    /// <param name="user"></param>
    /// <returns>Signs the new user in by issuing an authentication cookie</returns>
    /// <response code="200">Successful user registration</response>
    [AllowAnonymous]
    [HttpPost("register-user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAccountAsync(UserModel user)
    {
        var userToAdd = new User
        {
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName
        };

        var entry = _db.Users.Add(userToAdd);
        await _db.SaveChangesAsync();

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(
            identity.NameClaimType,
            $"{entry.Entity.Id}",
            ClaimValueTypes.UInteger64
        ));

        //go ahead and sign the user although no credentials registered yet
        return SignIn(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Sign out the current user
    /// </summary>
    /// <returns>Signs out the user by clearing the authentication cookie</returns>
    /// <response code="200">Successful logout</response>
    [HttpPost("logout")]
    public SignOutResult Logout()
    {
        return SignOut(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
