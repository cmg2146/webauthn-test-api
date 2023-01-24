namespace WebAuthnTest.Api;

using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using WebAuthnTest.Database;

[ApiController]
[Route("users")]
[Produces("application/json")]
public class UsersController : Controller
{
    private readonly WebAuthnTestDbContext _db;

    public UsersController(
        WebAuthnTestDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieve a user
    /// </summary>
    /// <returns>The user with the specified ID</returns>
    /// <response code="200">Returns the user</response>
    /// <response code="403">If the requestor is forbidden from retrieving the user</response>
    /// <response code="404">If the user is not found</response>
    [HttpGet("{userId}")]
    [ActionName(nameof(GetUserAsync))] //prevents asp.net from auto-stripping "Async"
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserModel>> GetUserAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        if (User.Identity!.UserId() != userId)
        {
            return Forbid();
        }

        var user = await _db
            .Users
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        if (user == null)
        {
            return NotFound("User not found");
        }

        return new UserModel
        {
            Id = user.Id,
            Created = user.Created,
            Updated = user.Updated,
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }

    /// <summary>
    /// Create a user
    /// </summary>
    /// <returns>The newly created user</returns>
    /// <response code="201">Returns the new user</response>
    [HttpPost("")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserModel>> CreateUserAsync(
        UserModel user,
        CancellationToken cancellationToken)
    {
        var userToAdd = new User
        {
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName
        };

        var entry = _db.Users.Add(userToAdd);
        await _db.SaveChangesAsync(cancellationToken);

        user.Id = entry.Entity.Id;
        user.Created = entry.Entity.Created;
        user.Updated = entry.Entity.Updated;

        return CreatedAtAction(
            nameof(GetUserAsync),
            new { userId = user.Id },
            user);
    }

    /// <summary>
    /// Update a user
    /// </summary>
    /// <returns>The updated user</returns>
    /// <response code="200">Returns the user</response>
    /// <response code="403">If the requestor is forbidden from updating the user</response>
    /// <response code="404">If the user is not found</response>
    [HttpPut("{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserAsync(
        long userId,
        UserModel user,
        CancellationToken cancellationToken)
    {
        if (User.Identity!.UserId() != userId)
        {
            return Forbid();
        }

        var updatedUser = await _db
            .Users
            .AsTracking()
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        if (updatedUser == null)
        {
            return NotFound();
        }

        updatedUser.DisplayName = user.DisplayName;
        updatedUser.FirstName = user.FirstName;
        updatedUser.LastName = user.LastName;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Retrieve my info
    /// </summary>
    /// <returns>The user's info</returns>
    /// <response code="200">Returns the user</response>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserModel>> GetMyInfo(CancellationToken cancellationToken)
    {
        var userId = User.Identity!.UserId();

        var user = await _db
            .Users
            .SingleOrDefaultAsync(t => t.Id == userId, cancellationToken);

        //should never happen
        if (user == null)
        {
            return NotFound();
        }

        return new UserModel
        {
            Id = user.Id,
            Created = user.Created,
            Updated = user.Updated,
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }

    /// <summary>
    /// Retrieve my credentials
    /// </summary>
    /// <returns>My credentials</returns>
    /// <response code="200">Returns the credentials</response>
    [HttpGet("me/credentials")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IAsyncEnumerable<UserCredentialModel> GetMyCredentials()
    {
        var userId = User.Identity!.UserId();

        var credentials = _db
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

        return credentials;
    }

    /// <summary>
    /// Get the credential used to authenticate my current login session
    /// </summary>
    /// <returns>The current session's credential</returns>
    /// <response code="200">Returns the credential</response>
    /// <response code="404">The logged on user does not have any FIDO credentials setup yet</response>
    [HttpGet("me/credentials/current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserCredentialModel?>> GetActiveCredentialAsync(
        CancellationToken cancellationToken)
    {
        var credentialIdClaim = (User.Identity as ClaimsIdentity)
            !.FindFirst(WebAuthnClaimConstants.UserCredentialIdClaimType);

        if (long.TryParse(credentialIdClaim?.Value, out long credentialId))
        {
            var credential = await _db
                .UserCredentials
                .Select(t => new UserCredentialModel
                {
                    Id = t.Id,
                    Created = t.Created,
                    Updated = t.Updated,
                    UserId = t.UserId,
                    DisplayName = t.DisplayName,
                    AttestationFormatId = t.AttestationFormatId
                })
                .SingleOrDefaultAsync(t => t.Id == credentialId, cancellationToken);

            return credential;
        }
        else
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Delete one of my credentials
    /// </summary>
    /// <response code="204">The credential was successfully deleted</response>
    /// <response code="404">The credential does not exist</response>
    [HttpDelete("me/credentials/{credentialId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMyCredentialAsync(
        long credentialId,
        CancellationToken cancellationToken)
    {
        var userId = User.Identity!.UserId();

        var credential = await _db
            .UserCredentials
            .Select(t => new
            {
                Id = t.Id,
                CredentialIdHash = t.CredentialIdHash,
                UserId = t.UserId
            })
            .SingleOrDefaultAsync(t => t.Id == credentialId, cancellationToken);

        //return not found if the credential belongs to another user - dont want to leak any information
        if (credential == null || credential.UserId != userId)
        {
            return NotFound();
        }

        _db.UserCredentials.Remove(new UserCredential
        {
            Id = credentialId,
            //must specfiy this here because its an alternate key or EF can't track the entity.
            CredentialIdHash = credential.CredentialIdHash
        });
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
