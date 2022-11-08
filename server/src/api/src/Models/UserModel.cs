namespace WebAuthnTest.Api;

using System.ComponentModel.DataAnnotations;

public class UserModel : ModelBase
{
    [Required]
    [StringLength(255)]
    public string DisplayName { get; set; } = null!;
    [Required]
    [StringLength(255)]
    public string FirstName { get; set; } = null!;
    [Required]
    [StringLength(255)]
    public string LastName { get; set; } = null!;
}