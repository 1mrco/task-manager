namespace TaskManagerAPI.DTOs.Task;

public class TaskResponseDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UserId { get; set; }
}
