namespace WebAuthnTest.Api;

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("users")]
[Produces("application/json")]
public class UsersController : Controller
{
    private readonly UserService _userService;

    public UsersController(
        UserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Retrieve a user
    /// </summary>
    /// <param name="userId">The ID of the user to retrieve</param>
    /// <param name="cancellationToken"></param>
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

        var user = await _userService.GetUserAsync(userId, cancellationToken);

        if (user == null)
        {
            return NotFound("User not found");
        }

        return user;
    }

    /// <summary>
    /// Create a user
    /// </summary>
    /// <param name="user">The user to create</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The newly created user</returns>
    /// <response code="201">Returns the new user</response>
    [HttpPost("")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserModel>> CreateUserAsync(
        UserModelCreate user,
        CancellationToken cancellationToken)
    {
        var newUser = await _userService.CreateUserAsync(
            user,
            cancellationToken: cancellationToken);

        return CreatedAtAction(
            nameof(GetUserAsync),
            new { userId = newUser.Id },
            newUser);
    }

    /// <summary>
    /// Update a user
    /// </summary>
    /// <param name="userId">The ID of the user to update</param>
    /// <param name="user">The updated user data</param>
    /// <param name="cancellationToken"></param>
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
        UserModelUpdate user,
        CancellationToken cancellationToken)
    {
        if (User.Identity!.UserId() != userId)
        {
            return Forbid();
        }

        var updated = await _userService.UpdateUserAsync(userId, user, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

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
        var user = await _userService.GetUserAsync(userId, cancellationToken);

        //should never happen
        if (user == null)
        {
            return NotFound();
        }

        return user;
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

        return _userService.GetUserCredentials(userId);
    }

    /// <summary>
    /// Get the credential used to authenticate my current login session
    /// </summary>
    /// <returns>The current session's credential</returns>
    /// <response code="404">Could not retrieve the credential associated with the current login session</response>
    [HttpGet("me/credentials/current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserCredentialModel>> GetActiveCredentialAsync(
        CancellationToken cancellationToken)
    {
        var credentialId = (User.Identity as ClaimsIdentity).GetActiveCredentialId();

        // should never happen
        if (credentialId == null)
        {
            return NotFound();
        }

        var userId = User.Identity!.UserId();
        var credential = await _userService.GetUserCredentialAsync(
            userId,
            credentialId.Value,
            cancellationToken);

        if (credential == null)
        {
            return NotFound();
        }

        return credential;
    }

    /// <summary>
    /// Delete one of my credentials
    /// </summary>
    /// <param name="credentialId">The ID of the credential to delete</param>
    /// <param name="cancellationToken"></param>
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

        var credentialFound = await _userService.DoesCredentialExistAsync(
            userId, credentialId, cancellationToken
        );
        if (!credentialFound)
        {
            return NotFound();
        }

        var deleted = await _userService.DeleteUserCredentialAsync(userId, credentialId, cancellationToken);
        if (!deleted)
        {
            return Conflict();
        }

        return NoContent();
    }
}
