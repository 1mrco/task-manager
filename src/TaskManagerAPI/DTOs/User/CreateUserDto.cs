using System.ComponentModel.DataAnnotations;

namespace TaskManagerAPI.DTOs.User;

public class CreateUserDto
{
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [MinLength(8)]
    public required string Password { get; set; }
}
