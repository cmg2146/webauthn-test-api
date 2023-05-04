namespace WebAuthnTest.Api;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    private readonly WebAuthnTestDbContext _db;
    private readonly UserService _userService;

    private const string ATTESTATION_OPTIONS_SESSION_KEY = "webAuthn.credentialCreateOptions";
    private const string ASSERTION_OPTIONS_SESSION_KEY = "webAuthn.credentialAssertionOptions";

    public WebAuthnController(
        WebAuthnTestDbContext db,
        UserService userService,
        IFido2 fido2)
    {
        _db = db;
        _userService = userService;
        _fido2 = fido2;
    }

    private static string FormatException(Exception e)
    {
        return e.InnerException?.Message == null
            ? e.Message
            : $"{e.Message} ({e.InnerException.Message})";
    }

    /// <summary>
    /// Begin device registration for a new user - retrieve credential creation options
    /// to start WebAuthn registration ceremony
    /// </summary>
    /// <returns>The credential creation options</returns>
    /// <response code="200">Returns the options</response>
    /// <response code="500">Problem generating options</response>
    [AllowAnonymous]
    [HttpPost("signup-start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CredentialCreateOptions>> GetInitialCredentialCreationOptionsAsync(
        UserModelCreate user,
        CancellationToken cancellationToken)
    {
        try
        {
            var userModel = new UserModel
            {
                DisplayName = user.DisplayName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserHandle = UserHandleConvert.NewUserHandle()
            };

            var credentialCreationOptions = _userService.GetCredentialCreateOptions(
                userModel,
                new List<byte[]>());

            await HttpContext.Session.SetStringAsync(
                ATTESTATION_OPTIONS_SESSION_KEY,
                credentialCreationOptions.ToJson(),
                cancellationToken);

            return credentialCreationOptions;
        }
        catch (Exception e)
        {
            return Problem(FormatException(e), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Complete device registration for a new user - post authenticator attestation response to complete
    /// WebAuthn registration ceremony
    /// </summary>
    /// <returns>The new credential info</returns>
    /// <response code="200">Signs the user in with an authentication cookie</response>
    /// <response code="401">There was an issue validating the authenticator attestation response</response>
    [AllowAnonymous]
    [HttpPost("signup-finish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateInitialCredentialAsync(
        AuthenticatorAttestationRawResponse attestationResponse,
        CancellationToken cancellationToken)
    {
        Fido2.CredentialMakeResult credentialCreateResult;

        try
        {
            //get the credential creation options we originally sent to client
            var originalCreationOptions = CredentialCreateOptions.FromJson(
                await HttpContext.Session.GetStringAsync(
                    ATTESTATION_OPTIONS_SESSION_KEY,
                    cancellationToken
                )
            );

            credentialCreateResult = await _fido2.MakeNewCredentialAsync(
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

        var attestationResult = credentialCreateResult.Result!;
        var credential = await _userService.GenerateCredentialAsync(
            attestationResult,
            cancellationToken);

        var user = new UserModelCreate
        {
            DisplayName = attestationResult.User.Name,
            FirstName = attestationResult.User.DisplayName.Split(" ")[0],
            LastName = attestationResult.User.DisplayName.Split(" ")[1]
        };

        await _userService.CreateUserAsync(
            user,
            userHandle: attestationResult.User.Id,
            credential: credential,
            cancellationToken);

        return this.SignInWithUserCredential(credential);
    }

    /// <summary>
    /// Begin device registration for existing user - retrieve credential creation options
    /// to start WebAuthn registration ceremony
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
        var user = await _userService.GetUserAsync(userId, cancellationToken);

        //should never happen
        if (user == null)
        {
            return Unauthorized();
        }

        try
        {
            var existingCredentials = _db
                .UserCredentials
                .Where(t => t.UserId == userId)
                .Select(t => t.CredentialId)
                .AsEnumerable();

            var credentialCreationOptions = _userService.GetCredentialCreateOptions(
                user,
                existingCredentials);

            await HttpContext.Session.SetStringAsync(
                ATTESTATION_OPTIONS_SESSION_KEY,
                credentialCreationOptions.ToJson(),
                cancellationToken);

            return credentialCreationOptions;
        }
        catch (Exception e)
        {
            return Problem(FormatException(e), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Complete device registration for existing user - post authenticator attestation response to
    /// complete WebAuthn registration ceremony
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
            var originalCreationOptions = CredentialCreateOptions.FromJson(
                await HttpContext.Session.GetStringAsync(
                    ATTESTATION_OPTIONS_SESSION_KEY,
                    cancellationToken
                )
            );

            credentialCreateResult = await _fido2.MakeNewCredentialAsync(
                attestationResponse,
                originalCreationOptions,
                // we have a unique index in the database so this would be redundant
                async (args, _) => await Task.FromResult(true),
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            return Unauthorized(FormatException(e));
        }

        var attestationResult = credentialCreateResult.Result!;
        var userCredentialToAdd = await _userService.GenerateCredentialAsync(
            attestationResult,
            cancellationToken);

        //TODO: Delete existing credential if it has same Id?
        _db.UserCredentials.Add(userCredentialToAdd);
        await _db.SaveChangesAsync(cancellationToken);

        // System.Text.Json cannot serialize these properly. See https://github.com/passwordless-lib/fido2-net-lib/issues/328
        attestationResult.AttestationCertificate = null;
        attestationResult.AttestationCertificateChain = null;

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
    public async Task<ActionResult<AssertionOptions>> GetCredentialRequestOptionsAsync(
        CancellationToken cancellationToken
    )
    {
        try
        {
            var credentialAssertionOptions = _userService.GetCredentialAssertionOptions(new List<byte[]>());

            await HttpContext.Session.SetStringAsync(
                ASSERTION_OPTIONS_SESSION_KEY,
                credentialAssertionOptions.ToJson(),
                cancellationToken);

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
        var credential = await _userService.GetUserCredentialRawAsync(
            assertionResponse.Response.UserHandle,
            assertionResponse.Id,
            cancellationToken);

        if (credential == null)
        {
            return Unauthorized();
        }

        AssertionVerificationResult assertionVerificationResult;
        try
        {
            // get the assertion options we orignally sent the client
            var originalRequestOptions = AssertionOptions.FromJson(
                await HttpContext.Session.GetStringAsync(
                    ASSERTION_OPTIONS_SESSION_KEY,
                    cancellationToken
                )
            );

            assertionVerificationResult = await _fido2.MakeAssertionAsync(
                assertionResponse,
                originalRequestOptions,
                credential.PublicKey,
                credential.SignatureCounter,
                // we have already checked the credential belongs to the user
                async (args, _) => await Task.FromResult(true),
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            return Unauthorized(FormatException(e));
        }

        // update the counter
        _db.Attach(credential);
        credential.SignatureCounter = assertionVerificationResult.Counter;
        await _db.SaveChangesAsync(cancellationToken);

        return this.SignInWithUserCredential(credential);
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
