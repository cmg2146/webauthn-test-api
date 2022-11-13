namespace WebAuthnTest.Api;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;
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
[Route("api/webauthn")]
public class WebAuthnController : Controller
{
    private IFido2 _fido2;
    private WebAuthnTestDbContext _db;

    public WebAuthnController(
        WebAuthnTestDbContext db,
        IFido2 fido2)
    {
        _db = db;
        _fido2 = fido2;
    }

    private string FormatException(Exception e)
    {
        return string.Format("{0}{1}", e.Message, e.InnerException != null ? " (" + e.InnerException.Message + ")" : "");
    }

    [HttpGet("register")]
    public async Task<IActionResult> GetCredentialCreationOptionsAsync(
        AuthenticatorAttachment? authType,
        CancellationToken cancellationToken)
    {
        var userId = User.Identity!.UserId();

        var user  = await _db
            .Users
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        if (user == null)
        {
            return Unauthorized();
        }

        try
        {
            var authenticatorSelection = new AuthenticatorSelection
            {
                UserVerification = UserVerificationRequirement.Required,
                AuthenticatorAttachment = authType
            };

            var exts = new AuthenticationExtensionsClientInputs() 
            { 
                Extensions = true, 
                UserVerificationMethod = true, 
            };

            var fido2User = new Fido2User
            {
                Name = user.DisplayName,
                DisplayName = user.DisplayName,
                Id = BitConverter.GetBytes(user.Id)
            };

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
                    AttestationConveyancePreference.Indirect,
                    exts);

            HttpContext.Session.SetString("webAuthn.credentialCreateOptions", credentialCreationOptions.ToJson());

            return Ok(credentialCreationOptions);
        }
        catch (Exception e)
        {
            return Problem(FormatException(e), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> CreateCredentialAsync(
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

        //TODO: Delete existing credential if it has same Id?
        var userCredentialToAdd = new UserCredential
        {
            UserId = userId,
            CredentialId = credentialCreateResult.Result!.CredentialId,
            PublicKey = credentialCreateResult.Result.PublicKey,
            AttestationFormatId = credentialCreateResult.Result.CredType,
            AaGuid = credentialCreateResult.Result.Aaguid,
            DisplayName = credentialCreateResult.Result.CredType,
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

        return Ok(credentialCreateResult);
    }


    [HttpGet("active-credential")]
    public async Task<IActionResult> GetActiveCredentialAsync(
        CancellationToken cancellationToken)
    {
        var credentialIdClaim = (User.Identity as ClaimsIdentity)
            !.FindFirst("userCredentialId");

        if (long.TryParse(credentialIdClaim?.Value, out long credentialId))
        {
            var credential = await _db
                .UserCredentials
                .SingleOrDefaultAsync(t => t.Id == credentialId, cancellationToken);

            return Ok(credential);
        }
        else
        {
            return NotFound();
        }
    }

    [AllowAnonymous]
    [HttpGet("authenticate")]
    public IActionResult GetCredentialRequestOptions()
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

            return Ok(credentialAssertionOptions);
        }

        catch (Exception e)
        {
            return Problem(FormatException(e), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [AllowAnonymous]
    [HttpPost("authenticate")]
    public async Task<IActionResult> AuthenticateCredentialAsync(
        AuthenticatorAssertionRawResponse assertionResponse,
        CancellationToken cancellationToken)
    {
        var userId = BitConverter.ToInt64(assertionResponse.Response.UserHandle);
        var user = await _db
            .Users
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        if (user == null)
        {
            return Unauthorized();
        }

        var credential = await _db
            .UserCredentials
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
            _db.Update(credential);           
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            return Unauthorized(FormatException(e));
        }

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(
            identity.NameClaimType,
            userId.ToString(),
            ClaimValueTypes.UInteger64
        ));
        identity.AddClaim(new Claim(
            ClaimTypes.AuthenticationMethod,
            "webauthn"
        ));
        identity.AddClaim(new Claim(
            "userCredentialId",
            credential.Id.ToString(),
            ClaimValueTypes.UInteger64
        ));
                
        return SignIn(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpPost("register-user")]
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
            entry.Entity.Id.ToString(),
            ClaimValueTypes.UInteger64
        ));

        //go ahead and sign the user although no credentials registered yet
        return SignIn(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);        
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return SignOut(CookieAuthenticationDefaults.AuthenticationScheme);
    }    
}
