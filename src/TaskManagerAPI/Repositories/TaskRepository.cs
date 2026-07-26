using Microsoft.EntityFrameworkCore;
using TaskManagerAPI.Data;
using TaskManagerAPI.Models;

namespace TaskManagerAPI.Repositories;

public class TaskRepository(AppDbContext context) : ITaskRepository
{
    public async Task<IEnumerable<TaskItem>> GetAllAsync() =>
        await context.Tasks.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync();

    public async Task<TaskItem?> GetByIdAsync(int id) =>
        await context.Tasks.FindAsync(id);

    public async Task<IEnumerable<TaskItem>> GetTasksByUserIdAsync(int userId) =>
        await context.Tasks
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<TaskItem> AddAsync(TaskItem task)
    {
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    public async Task UpdateAsync(TaskItem task)
    {
        context.Tasks.Update(task);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(TaskItem task)
    {
        context.Tasks.Remove(task);
        await context.SaveChangesAsync();
    }
}
