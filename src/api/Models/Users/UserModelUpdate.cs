using System.ComponentModel.DataAnnotations;
using WebAuthnTest.Database;

namespace WebAuthnTest.Api;

public class UserModelUpdate : IUserModelBase
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
