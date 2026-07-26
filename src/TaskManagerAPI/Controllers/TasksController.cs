using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagerAPI.DTOs.Task;
using TaskManagerAPI.Models;
using TaskManagerAPI.Models.Enums;
using TaskManagerAPI.Repositories;

namespace TaskManagerAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController(ITaskRepository taskRepository, IUserRepository userRepository) : ControllerBase
{
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private bool IsAdmin =>
        User.IsInRole(UserRole.Admin.ToString());

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponseDto>>> GetAll(
        [FromQuery] TaskItemStatus? status,
        [FromQuery] string? search,
        [FromQuery] int? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        IEnumerable<TaskItem> tasks;

        if (IsAdmin)
        {
            if (userId.HasValue && userId.Value > 0)
            {
                tasks = await taskRepository.GetTasksByUserIdAsync(userId.Value);
            }
            else
            {
                tasks = await taskRepository.GetAllAsync();
            }
        }
        else
        {
            tasks = await taskRepository.GetTasksByUserIdAsync(CurrentUserId);
        }

        if (status.HasValue)
        {
            tasks = tasks.Where(t => t.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var query = search.Trim();
            tasks = tasks.Where(t =>
                t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (t.Description != null && t.Description.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        var pagedTasks = tasks
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToResponseDto);

        return Ok(pagedTasks);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskResponseDto>> GetById(int id)
    {
        var task = await taskRepository.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound(new { message = "Task not found." });
        }

        if (!IsAdmin && task.UserId != CurrentUserId)
        {
            return Forbidden();
        }

        return Ok(MapToResponseDto(task));
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponseDto>> Create([FromBody] CreateTaskDto dto)
    {
        int targetUserId = CurrentUserId;

        if (IsAdmin && dto.UserId.HasValue && dto.UserId.Value > 0)
        {
            var userExists = await userRepository.GetByIdAsync(dto.UserId.Value);
            if (userExists == null)
            {
                return BadRequest(new { message = $"User with ID {dto.UserId.Value} does not exist." });
            }
            targetUserId = dto.UserId.Value;
        }

        var task = new TaskItem
        {
            Title = dto.Title,
            Description = dto.Description,
            DueDate = dto.DueDate,
            Status = TaskItemStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UserId = targetUserId
        };

        var createdTask = await taskRepository.AddAsync(task);
        return CreatedAtAction(nameof(GetById), new { id = createdTask.Id }, MapToResponseDto(createdTask));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskDto dto)
    {
        var task = await taskRepository.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound(new { message = "Task not found." });
        }

        if (!IsAdmin && task.UserId != CurrentUserId)
        {
            return Forbidden();
        }

        if (!string.IsNullOrWhiteSpace(dto.Title))
        {
            task.Title = dto.Title;
        }

        if (dto.Description != null)
        {
            task.Description = dto.Description;
        }

        if (dto.Status.HasValue)
        {
            task.Status = dto.Status.Value;
        }

        if (dto.DueDate.HasValue)
        {
            task.DueDate = dto.DueDate.Value;
        }

        if (IsAdmin && dto.UserId.HasValue && dto.UserId.Value > 0)
        {
            var userExists = await userRepository.GetByIdAsync(dto.UserId.Value);
            if (userExists == null)
            {
                return BadRequest(new { message = $"User with ID {dto.UserId.Value} does not exist." });
            }
            task.UserId = dto.UserId.Value;
        }

        await taskRepository.UpdateAsync(task);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await taskRepository.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound(new { message = "Task not found." });
        }

        if (!IsAdmin && task.UserId != CurrentUserId)
        {
            return Forbidden();
        }

        await taskRepository.DeleteAsync(task);
        return NoContent();
    }

    private static TaskResponseDto MapToResponseDto(TaskItem task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status.ToString(),
        DueDate = task.DueDate,
        CreatedAt = task.CreatedAt,
        UserId = task.UserId
    };

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to access or modify this task." });
}
