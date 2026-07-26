using System.ComponentModel.DataAnnotations;
using TaskManagerAPI.Models.Enums;

namespace TaskManagerAPI.DTOs.Task;
public class UpdateTaskDto
{
    [MaxLength(200)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    public TaskItemStatus? Status { get; set; }

    public DateTime? DueDate { get; set; }

    public int? UserId { get; set; }
}
