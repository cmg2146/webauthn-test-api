namespace WebAuthnTest.Api;

using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using WebAuthnTest.Database;

[ApiController]
[Route("api/users")]
public class UsersController : Controller
{
    private WebAuthnTestDbContext _db;

    public UsersController(
        WebAuthnTestDbContext db)
    {
        _db = db;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserAsync(long userId)
    {
        if (User.Identity?.Name != userId.ToString())
        {
            return Forbid();
        }

        var user = await _db.Users.FindAsync(userId);

        //should never happen
        if (user == null)
        {
            return NotFound("User not found");
        }

        return Ok(new UserModel
        {
            Id = user.Id,
            Created = user.Created,
            Updated = user.Updated,
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }

    [HttpPost("")]
    public async Task<IActionResult> PostUserAsync(UserModel user)
    {
        var userToAdd = new User
        {
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName
        };

        var entry = _db.Users.Add(userToAdd);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetUserAsync),
            new { userId = entry.Entity.Id },
            entry.Entity);
    }

    [HttpGet("{userId}/credentials")]
    public IActionResult GetUserCredentials(long userId)
    {
        if (User.Identity?.Name != userId.ToString())
        {
            return Forbid();
        }

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
            .AsAsyncEnumerable();

        return Ok(credentials);
    }    

    [HttpDelete("{userId}/credentials/{credentialId}")]
    public async Task<IActionResult> DeleteUserCredentialAsync(
        long userId,
        long credentialId)
    {
        if (User.Identity?.Name != userId.ToString())
        {
            return Forbid();
        }

        var credential = await _db
            .UserCredentials
            .Where(t => t.Id == credentialId)
            .Select(t => new
            {
                Id = t.Id,
                UserId = t.UserId
            })
            .SingleOrDefaultAsync();

        //return not found if the credential belongs to another user - dont want to leak any information
        if (credential == null || credential.UserId != userId)
        {
            return NotFound("Credential not found");
        }

        _db.UserCredentials.Remove(new UserCredential { Id = credentialId });
        await _db.SaveChangesAsync();

        return NoContent();
    }    
}
