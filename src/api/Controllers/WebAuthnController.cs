namespace WebAuthnTest.Api;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Fido2NetLib;
using Fido2NetLib.Objects;
using WebAuthnTest.Database;


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

    [HttpGet("{userId}/register")]
    public async Task<IActionResult> GetRegistrationOptionsAsync(
        long userId,
        AttestationConveyancePreference attestationType,
        AuthenticatorAttachment? authType,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.Name != userId.ToString())
        {
            return Forbid();
        }

        try
        {
            var user  = await _db
                .Users
                .FindAsync(userId, cancellationToken);

            if (user == null)
            {
                return NotFound();
            }

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
                .Entry(user)
                .Collection(t => t.UserCredentials)
                .Query()
                .Select(t => new PublicKeyCredentialDescriptor(t.CredentialId))
                .ToListAsync(cancellationToken);               
            
            var credentialCreationOptions = _fido2
                .RequestNewCredential(
                    fido2User,
                    existingCredentials,
                    authenticatorSelection,
                    attestationType,
                    exts);

            HttpContext.Session.SetString("webAuthn.credentialCreateOptions", credentialCreationOptions.ToJson());

            return Json(credentialCreationOptions);
        }
        catch (Exception e)
        {
            return Json(new CredentialCreateOptions { Status = "error", ErrorMessage = FormatException(e) });
        }
    }

    [HttpPost("{userId}/register")]
    public async Task<IActionResult> RegisterAsync(
        long userId,
        AuthenticatorAttestationRawResponse attestationResponse,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.Name != userId.ToString())
        {
            return Forbid();
        }

        try
        {
            //get the credential creation options we originally sent to client
            var originalCreationOptions = CredentialCreateOptions
                .FromJson(HttpContext.Session.GetString("webAuthn.credentialCreateOptions"));

            var credentialCreateResult = await _fido2
                .MakeNewCredentialAsync(
                    attestationResponse,
                    originalCreationOptions,
                    //we have a unique index in the database so this would be redundant
                    async (args, _) => await Task.FromResult(true),
                    cancellationToken: cancellationToken);

            _db.UserCredentials.Add(new UserCredential
            {
                UserId = userId,
                CredentialId = credentialCreateResult.Result!.CredentialId,
                PublicKey = credentialCreateResult.Result.PublicKey,
                AttestationFormatId = credentialCreateResult.Result.CredType,
                AaGuid = credentialCreateResult.Result.Aaguid,
                DisplayName = credentialCreateResult.Result.AttestationCertificate!.FriendlyName,
                SignatureCounter = credentialCreateResult.Result.Counter
            });

            await _db.SaveChangesAsync(cancellationToken);

            // System.Text.Json cannot serialize these properly. See https://github.com/passwordless-lib/fido2-net-lib/issues/328
            credentialCreateResult.Result.AttestationCertificate = null;
            credentialCreateResult.Result.AttestationCertificateChain = null;

            return Json(credentialCreateResult);
        }
        catch (Exception e)
        {
            return Json(new Fido2.CredentialMakeResult(status: "error", errorMessage: FormatException(e), result: null));
        }
    }

    [AllowAnonymous]
    [HttpGet("authenticate")]
    public IActionResult GetAuthenticationOptions()
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

            return Json(credentialAssertionOptions);
        }

        catch (Exception e)
        {
            return Json(new AssertionOptions { Status = "error", ErrorMessage = FormatException(e) });
        }
    }

    [AllowAnonymous]
    [HttpPost("authenticate")]
    public async Task<IActionResult> AuthenticateAsync(
        AuthenticatorAssertionRawResponse assertionResponse,
        CancellationToken cancellationToken)
    {
        try
        {
            // get the assertion options we orignally sent the client
            var options = AssertionOptions.FromJson(
                HttpContext.Session.GetString("webAuthn.credentialAssertionOptions"));

            var userId = BitConverter.ToInt64(assertionResponse.Response.UserHandle);
            var user = await _db
                .Users
                .FindAsync(userId, cancellationToken);

            if (user == null)
            {
                throw new Exception("User not found");
            }

            var credential = await _db
                .Entry(user)
                .Collection(t => t.UserCredentials)
                .Query()
                .FirstOrDefaultAsync(t => t.CredentialId == assertionResponse.Id, cancellationToken);

            if (credential == null)
            {
                throw new Exception("Credential not found");
            }

            var assertionVerificationResult = await _fido2
                .MakeAssertionAsync(
                    assertionResponse,
                    options,
                    credential.PublicKey,
                    credential.SignatureCounter,
                    async (args, _) => await Task.FromResult(credential.UserId == userId),
                    cancellationToken: cancellationToken);

            // update the counter
            credential.SignatureCounter = assertionVerificationResult.Counter;
            _db.Update(credential);           
            await _db.SaveChangesAsync(cancellationToken);

            //sign the user in
            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

            await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

            return Json(assertionVerificationResult);
        }
        catch (Exception e)
        {
            return Json(new AssertionVerificationResult { Status = "error", ErrorMessage = FormatException(e) });
        }
    }
}
