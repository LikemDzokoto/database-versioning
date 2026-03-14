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
        var todo = await _context.TodoItems
            .Where(t => t.EntityId == entityId && t.IsCurrent)
            .FirstOrDefaultAsync();

        if (todo == null) return NotFound();

        todo.Title = request.NewTitle;
        todo.IsCompleted = request.IsCompleted;
        todo.ValidFrom = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(todo);
    }

}