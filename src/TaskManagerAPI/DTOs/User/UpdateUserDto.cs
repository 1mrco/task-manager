using System.ComponentModel.DataAnnotations;

namespace TaskManagerAPI.DTOs.User;

public class UpdateUserDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [MinLength(8)]
    public string? Password { get; set; }

    public TaskManagerAPI.Models.Enums.UserRole? Role { get; set; }
}
