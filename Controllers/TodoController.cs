using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseVersion.Api.Data;
using DatabaseVersion.Api.Models;

namespace DatabaseVersion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodoController : ControllerBase
{
    private readonly AppDbContext _context;

    public TodoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetCurrentTodos()
    {
        return await _context.TodoItems
            .Where(t => t.IsCurrent)
            .ToListAsync();
    }


    [HttpGet("{entityId}/history")]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetHistory(Guid entityId)
    {
        return await _context.TodoItems
            .Where(t => t.EntityId == entityId)
            .OrderByDescending(t => t.Version)
            .ToListAsync();
    }


    [HttpPost]
    public async Task<ActionResult<TodoItem>> CreateTodo([FromBody] TodoCreateRequest request)
    {
        var todo = new TodoItem
        {
            EntityId = Guid.NewGuid(),
            Version = 1,
            Title = request.Title,
            Description = request.Description,
            IsCurrent = true,
            ValidFrom = DateTime.UtcNow
        };

        _context.TodoItems.Add(todo);
        await _context.SaveChangesAsync();
        return Ok(todo);
    }

    [HttpPut("{entityId}")]
    public async Task<IActionResult> UpdateTodo(Guid entityId, [FromBody] TodoUpdateRequest request)
    {
        // Get the current version number for this entity
        var currentVersion = await _context.TodoItems
            .Where(t => t.EntityId == entityId)
            .MaxAsync(t => (int?)t.Version) ?? 0;

        // Create new version - trigger will handle IsCurrent automatically
        var newVersion = new TodoItem
        {
            EntityId = entityId,
            Version = currentVersion + 1,
            Title = request.NewTitle,
            Description = null, // Keep existing description or set to null
            IsCompleted = request.IsCompleted,
            IsCurrent = true, // This will be overridden by the trigger
            ValidFrom = DateTime.UtcNow
        };

        _context.TodoItems.Add(newVersion);
        await _context.SaveChangesAsync();

        return Ok(newVersion);
    }

}