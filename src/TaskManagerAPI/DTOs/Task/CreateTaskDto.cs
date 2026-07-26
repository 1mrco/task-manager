using System.ComponentModel.DataAnnotations;

namespace TaskManagerAPI.DTOs.Task;

public class CreateTaskDto
{
    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Admin-only: assign task to another user. Regular users get UserId from JWT.
    /// </summary>
    public int? UserId { get; set; }
}
