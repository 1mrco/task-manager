using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagerAPI.DTOs.User;
using TaskManagerAPI.Models.Enums;
using TaskManagerAPI.Repositories;

namespace TaskManagerAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(IUserRepository userRepository) : ControllerBase
{
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private bool IsAdmin =>
        User.IsInRole(UserRole.Admin.ToString());

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAll()
    {
        var users = await userRepository.GetAllAsync();
        var dtos = users.Select(u => new UserResponseDto
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Role = u.Role.ToString(),
            CreatedAt = u.CreatedAt
        });

        return Ok(dtos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponseDto>> GetById(int id)
    {
        if (!IsAdmin && CurrentUserId != id)
        {
            return Forbidden();
        }

        var user = await userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            CreatedAt = user.CreatedAt
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        if (!IsAdmin && CurrentUserId != id)
        {
            return Forbidden();
        }

        var user = await userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
        {
            var existingWithEmail = await userRepository.GetByEmailAsync(dto.Email);
            if (existingWithEmail != null && existingWithEmail.Id != id)
            {
                return Conflict(new { message = "Email is already taken by another user." });
            }
            user.Email = dto.Email;
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            user.Name = dto.Name;
        }

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        if (dto.Role.HasValue && IsAdmin)
        {
            user.Role = dto.Role.Value;
        }

        await userRepository.UpdateAsync(user);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin && CurrentUserId != id)
        {
            return Forbidden();
        }

        var user = await userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        await userRepository.DeleteAsync(user);
        return NoContent();
    }

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to access or modify this user." });
}
