namespace WebAuthnTest.Api;

using System.ComponentModel.DataAnnotations;
using WebAuthnTest.Database;

public class UserModel : ModelBase
{
    [Required]
    [StringLength(UserConfiguration.NAME_MAX_LENGTH)]
    public string DisplayName { get; set; } = null!;
    [Required]
    [StringLength(UserConfiguration.NAME_MAX_LENGTH)]
    public string FirstName { get; set; } = null!;
    [Required]
    [StringLength(UserConfiguration.NAME_MAX_LENGTH)]
    public string LastName { get; set; } = null!;
}
